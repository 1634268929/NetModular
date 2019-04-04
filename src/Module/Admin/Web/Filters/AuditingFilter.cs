﻿using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Filters;
using NetModular.Lib.Auth.Abstractions;
using NetModular.Lib.Utils.Core.Enums;
using NetModular.Module.Admin.Application.AuditInfoService;
using NetModular.Module.Admin.Domain.AuditInfo;
using NetModular.Module.Admin.Web.Attributes;
using Newtonsoft.Json;

namespace NetModular.Module.Admin.Web.Filters
{
    /// <summary>
    /// 审计过滤器
    /// </summary>
    public class AuditingFilter : IAsyncActionFilter
    {
        private readonly LoginInfo _loginInfo;
        private readonly IAuditInfoService _auditInfoService;

        public AuditingFilter(IAuditInfoService auditInfoService, LoginInfo loginInfo)
        {
            _auditInfoService = auditInfoService;
            _loginInfo = loginInfo;
        }

        public Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            if (CheckDisabled(context))
            {
                return next();
            }

            return ExecuteAuditing(context, next);
        }

        /// <summary>
        /// 执行审计功能
        /// </summary>
        /// <param name="context"></param>
        /// <param name="next"></param>
        /// <returns></returns>
        private async Task ExecuteAuditing(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var auditInfo = CreateAuditInfo(context);

            var sw = new Stopwatch();
            sw.Start();

            var resultContext = await next();

            sw.Stop();

            //执行结果
            auditInfo.Result = JsonConvert.SerializeObject(resultContext.Result);
            //用时
            auditInfo.ExecutionDuration = sw.ElapsedMilliseconds;

            await _auditInfoService.Add(auditInfo);
        }

        private AuditInfo CreateAuditInfo(ActionExecutingContext context)
        {
            var routeValues = context.ActionDescriptor.RouteValues;
            var auditInfo = new AuditInfo
            {
                AccountId = _loginInfo.AccountId,
                Area = routeValues["area"],
                Controller = routeValues["controller"],
                Action = routeValues["action"],
                Parameters = JsonConvert.SerializeObject(context.ActionArguments),
                Platform = _loginInfo.Platform,
                IP = _loginInfo.IP,
                ExecutionTime = DateTime.Now
            };

            //记录浏览器UA
            if (_loginInfo.Platform == Platform.Web)
            {
                auditInfo.BrowserInfo = context.HttpContext.Request.Headers["User-Agent"];
            }

            return auditInfo;
        }

        /// <summary>
        /// 判断是否禁用审计功能
        /// </summary>
        /// <param name="context"></param>
        /// <returns></returns>
        private bool CheckDisabled(ActionExecutingContext context)
        {
            return context.ActionDescriptor.EndpointMetadata.Any(m => m.GetType() == typeof(DisableAuditingAttribute));
        }
    }
}
