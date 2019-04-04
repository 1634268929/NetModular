﻿using System;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NetModular.Lib.Data.Abstractions;
using NetModular.Lib.Data.Abstractions.Entities;
using NetModular.Lib.Data.Abstractions.Options;
using NetModular.Lib.Data.Core;
using NetModular.Lib.Data.Core.Options;
using NetModular.Lib.Module.Abstractions;
using NetModular.Lib.Utils.Core.Extensions;
using NetModular.Lib.Utils.Core.Helpers;

namespace NetModular.Lib.Data.AspNetCore
{
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// 添加数据库
        /// </summary>
        /// <param name="services"></param>
        /// <param name="env"></param>
        /// <param name="modules"></param>
        public static void AddDb(this IServiceCollection services, IHostingEnvironment env, IModuleCollection modules)
        {
            var cfg = ConfigurationExtensions.Load("Db", env.EnvironmentName);
            var optionsCollection = cfg.Get<DbConnectionOptionsCollection>("Connections");

            if (!optionsCollection.Any() || !modules.Any())
                return;

            services.AddSingleton<IDbConnectionOptionsCollection>(optionsCollection);

            foreach (var options in optionsCollection)
            {
                var module = modules.FirstOrDefault(m => m.Id.Equals(options.Name, StringComparison.OrdinalIgnoreCase));

                LoadEntityTypes(module, options);

                services.AddDbContext(module, options);
            }
        }

        /// <summary>
        /// 添加数据库上下文
        /// </summary>
        private static void AddDbContext(this IServiceCollection services, ModuleInfo module, DbConnectionOptions options)
        {
            var dbContextType = module.AssembliesInfo.Infrastructure.GetTypes().FirstOrDefault(m => m.Name.EqualsIgnoreCase(options.Name + "DbContext"));
            if (dbContextType != null)
            {
                var dbContextOptionsAssemblyName = AssemblyHelper.GetCurrentAssemblyName().Replace("AspNetCore", "") + options.Dialect;
                var dbContextOptionsTypeName = options.Dialect + "DbContextOptions";

                var dbContextOptionsType = AssemblyLoadContext.Default.LoadFromAssemblyName(new AssemblyName(dbContextOptionsAssemblyName)).GetType($"{dbContextOptionsAssemblyName}.{dbContextOptionsTypeName}");

                //日志工厂
                var loggerFactory = services.BuildServiceProvider().GetService<ILoggerFactory>();
                //请求上下文访问器
                var httpContextAccessor = services.BuildServiceProvider().GetService<IHttpContextAccessor>();

                var contextOptions = (IDbContextOptions)Activator.CreateInstance(dbContextOptionsType, options, loggerFactory, httpContextAccessor);

                services.AddScoped(typeof(IDbContext), sp => Activator.CreateInstance(dbContextType, contextOptions));
                services.AddUnitOfWork(dbContextType);
                services.AddRepositories(module, options);
            }
        }

        /// <summary>
        /// 添加工作单元
        /// </summary>
        /// <param name="services"></param>
        /// <param name="dbContextType"></param>
        private static void AddUnitOfWork(this IServiceCollection services, Type dbContextType)
        {
            var serviceType = typeof(IUnitOfWork<>).MakeGenericType(dbContextType);
            var implementType = typeof(UnitOfWork<>).MakeGenericType(dbContextType);
            services.AddScoped(serviceType, implementType);
        }

        /// <summary>
        /// 添加仓储
        /// </summary>
        private static void AddRepositories(this IServiceCollection services, ModuleInfo module, DbConnectionOptions options)
        {
            var interfaceList = module.AssembliesInfo.Domain.GetTypes().Where(t => t.IsInterface && typeof(IRepository<>).IsImplementType(t)).ToList();

            if (!interfaceList.Any())
                return;

            //根据仓储的命名空间名称来注入不同数据库的仓储
            var entityNamespacePrefix = $"{module.AssembliesInfo.Infrastructure.GetName().Name}.Repositories.{options.Dialect}.";
            foreach (var serviceType in interfaceList)
            {
                var implementType = module.AssembliesInfo.Infrastructure.GetTypes().FirstOrDefault(m => m.FullName.NotNull() && m.FullName.StartsWith(entityNamespacePrefix) && serviceType.IsAssignableFrom(m));
                if (implementType != null)
                {
                    services.AddScoped(serviceType, sp =>
                    {
                        var dbContext = sp.GetServices<IDbContext>().FirstOrDefault(m => m.Options.Name.Equals(options.Name));
                        return Activator.CreateInstance(implementType, dbContext);
                    });
                }
            }
        }

        /// <summary>
        /// 加载实体类型列表
        /// </summary>
        /// <param name="module"></param>
        /// <param name="options"></param>
        private static void LoadEntityTypes(ModuleInfo module, DbConnectionOptions options)
        {
            options.EntityTypes = module.AssembliesInfo.Domain.GetTypes().Where(t => t.IsClass && typeof(IEntity).IsImplementType(t)).ToList();
        }
    }
}
