﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoMapper;
using NetModular.Lib.Data.Abstractions;
using NetModular.Lib.Data.Query;
using NetModular.Lib.Utils.Core.Result;
using NetModular.Module.Admin.Application.AccountService;
using NetModular.Module.Admin.Application.MenuService.ResultModels;
using NetModular.Module.Admin.Application.MenuService.ViewModels;
using NetModular.Module.Admin.Domain.AccountRole;
using NetModular.Module.Admin.Domain.Button;
using NetModular.Module.Admin.Domain.Menu;
using NetModular.Module.Admin.Domain.MenuPermission;
using NetModular.Module.Admin.Domain.Permission;
using NetModular.Module.Admin.Domain.RoleMenu;
using NetModular.Module.Admin.Domain.RoleMenuButton;
using NetModular.Module.Admin.Infrastructure.Repositories;

namespace NetModular.Module.Admin.Application.MenuService
{
    public class MenuService : IMenuService
    {
        private readonly IMapper _mapper;
        private readonly IUnitOfWork<AdminDbContext> _uow;
        private readonly IMenuRepository _menuRepository;
        private readonly IMenuPermissionRepository _menuPermissionRepository;
        private readonly IRoleMenuRepository _roleMenuRepository;
        private readonly IPermissionRepository _permissionRepository;
        private readonly IButtonRepository _buttonRepository;
        private readonly IRoleMenuButtonRepository _roleMenuButtonRepository;
        private readonly IAccountRoleRepository _accountRoleRepository;
        private readonly IAccountService _accountService;

        public MenuService(IMenuRepository menuRepository, IMenuPermissionRepository menuPermissionRepository, IUnitOfWork<AdminDbContext> uow, IMapper mapper, IRoleMenuRepository roleMenuRepository, IPermissionRepository permissionRepository, IButtonRepository buttonRepository, IRoleMenuButtonRepository roleMenuButtonRepository, IAccountRoleRepository accountRoleRepository, IAccountService accountService)
        {
            _menuRepository = menuRepository;
            _menuPermissionRepository = menuPermissionRepository;
            _uow = uow;
            _mapper = mapper;
            _roleMenuRepository = roleMenuRepository;
            _permissionRepository = permissionRepository;
            _buttonRepository = buttonRepository;
            _roleMenuButtonRepository = roleMenuButtonRepository;
            _accountRoleRepository = accountRoleRepository;
            _accountService = accountService;
        }

        public async Task<IResultModel> GetTree()
        {
            var treeModel = new List<MenuTreeResultModel>();

            var all = await _menuRepository.GetAllAsync();
            var father = all.Where(m => m.ParentId == default(Guid) && m.Level == 0)
                .OrderBy(m => m.Sort).ToList();

            foreach (var menu in father)
            {
                treeModel.Add(Menu2TreeModel(all, menu));
            }

            return ResultModel.Success(treeModel);
        }

        /// <summary>
        /// 菜单对象转换为树模型
        /// </summary>
        /// <param name="all"></param>
        /// <param name="menu"></param>
        /// <returns></returns>
        private MenuTreeResultModel Menu2TreeModel(IList<Menu> all, Menu menu)
        {
            var model = _mapper.Map<MenuTreeResultModel>(menu);

            if (!all.Any())
                return model;

            if (menu.Type == MenuType.Link)
                return model;

            var children = all.Where(m => m.ParentId == menu.Id).OrderBy(m => m.Sort).ToList();
            if (children.Any())
            {
                foreach (var child in children)
                {
                    model.Children.Add(Menu2TreeModel(all, child));
                }
            }

            return model;
        }

        public async Task<IResultModel> Add(MenuAddModel model)
        {
            var menu = _mapper.Map<Menu>(model);

            try
            {
                _uow.BeginTransaction();

                if (await _menuRepository.ExistsNameByParentId(menu.Name, menu.Id, menu.ParentId))
                {
                    _uow.Rollback();
                    return ResultModel.Failed($"节点名称“{menu.Name}”已存在");
                }

                //根据父节点的等级+1设置当前菜单的等级
                if (menu.ParentId != Guid.Empty)
                {
                    var parentMenu = await _menuRepository.GetAsync(model.ParentId);
                    if (parentMenu == null)
                    {
                        _uow.Rollback();
                        return ResultModel.Failed("父节点不存在");
                    }

                    menu.Level = parentMenu.Level + 1;
                }

                menu.RouteName = menu.RouteName?.ToLower();

                if (await _menuRepository.AddAsync(menu))
                {
                    _uow.Commit();
                    return ResultModel.Success();
                }

                _uow.Rollback();
                return ResultModel.Failed();
            }
            catch
            {
                _uow.Rollback();
                throw;
            }
        }

        public async Task<IResultModel> Delete(Guid id)
        {
            var entity = await _menuRepository.GetAsync(id);
            if (entity == null)
                return ResultModel.Failed("该节点不存在");

            if (await _menuRepository.ExistsChild(id))
                return ResultModel.Failed($"当前菜单({entity.Name})存在子菜单，请先删除子菜单");

            if (await _roleMenuRepository.ExistsWidthMenuId(id))
                return ResultModel.Failed($"有角色绑定了当前菜单({entity.Name})，请先删除关联信息");

            _uow.BeginTransaction();

            /*
             * 删除逻辑
             * 1、删除菜单
             * 2、删除该菜单与权限的关联信息
             */
            if (await _menuRepository.DeleteAsync(id)
                && await _menuPermissionRepository.DeleteByMenuId(id)
                && await _buttonRepository.DeleteByMenu(id)
                && await _roleMenuButtonRepository.DeleteByMenu(id))
            {
                _uow.Commit();

                await ClearAccountPermissionCache(id);

                return ResultModel.Success();
            }

            _uow.Rollback();
            return ResultModel.Failed();
        }

        public async Task<IResultModel> Edit(Guid id)
        {
            var entity = await _menuRepository.GetAsync(id);
            if (entity == null)
                return ResultModel.Failed("菜单不存在");

            var model = _mapper.Map<MenuUpdateModel>(entity);
            return ResultModel.Success(model);
        }

        public async Task<IResultModel> Update(MenuUpdateModel model)
        {
            var entity = await _menuRepository.GetAsync(model.Id);
            if (entity == null)
                return ResultModel.Failed("菜单不存在！");

            entity = _mapper.Map(model, entity);

            entity.RouteName = entity.RouteName.ToLower();
            var result = await _menuRepository.UpdateAsync(entity);
            return ResultModel.Result(result);
        }

        public async Task<IResultModel> Query(MenuQueryModel model)
        {
            var queryResult = new QueryResultModel<Menu>();

            var paging = model.Paging();

            queryResult.Rows = await _menuRepository.Query(paging, model.Name, model.RouteName, model.ParentId);
            queryResult.Total = paging.TotalCount;
            return ResultModel.Success(queryResult);
        }

        public async Task<IResultModel> Details(Guid id)
        {
            var entity = await _menuRepository.GetAsync(id);
            if (entity == null)
                return ResultModel.Failed("菜单不存在");

            return ResultModel.Success(entity);
        }

        public async Task<IResultModel> PermissionList(Guid id)
        {
            var exists = await _menuRepository.ExistsAsync(id);
            if (!exists)
                return ResultModel.Failed("菜单不存在！");

            var list = await _permissionRepository.QueryByMenu(id);
            return ResultModel.Success(list);
        }

        public async Task<IResultModel> BindPermission(MenuBindPermissionModel model)
        {
            _uow.BeginTransaction();

            //删除旧数据
            if (await _menuPermissionRepository.DeleteByMenuId(model.Id))
            {
                if (model.PermissionList == null || !model.PermissionList.Any())
                {
                    _uow.Commit();
                    await ClearAccountPermissionCache(model.Id);

                    return ResultModel.Success();
                }

                //添加新数据
                var entityList = new List<MenuPermission>();
                model.PermissionList.ForEach(p =>
                {
                    entityList.Add(new MenuPermission { MenuId = model.Id, PermissionId = p });
                });
                if (await _menuPermissionRepository.AddAsync(entityList))
                {
                    _uow.Commit();
                    await ClearAccountPermissionCache(model.Id);

                    return ResultModel.Success();
                }
            }

            _uow.Rollback();
            return ResultModel.Failed();
        }

        public async Task<IResultModel> ButtonList(Guid id)
        {
            var exists = await _menuRepository.ExistsAsync(id);
            if (!exists)
                return ResultModel.Failed("菜单不存在！");

            var list = await _buttonRepository.QueryByMenu(id);
            return ResultModel.Success(list);
        }

        /// <summary>
        /// 清除菜单关联账户的权限缓存
        /// </summary>
        /// <returns></returns>
        private async Task ClearAccountPermissionCache(Guid menuId)
        {
            var relationList = await _accountRoleRepository.QueryByMenu(menuId);
            if (relationList.Any())
            {
                foreach (var relation in relationList)
                {
                    _accountService.ClearPermissionListCache(relation.RoleId);
                }
            }
        }
    }
}
