﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NetModular.Lib.Data.Abstractions;
using NetModular.Lib.Data.Abstractions.Pagination;

namespace NetModular.Module.Admin.Domain.AuditInfo
{
    /// <summary>
    /// 审计信息仓储
    /// </summary>
    public interface IAuditInfoRepository : IRepository<AuditInfo>
    {
        Task<IList<AuditInfo>> Query(Paging paging, Guid? accountId, string moduleCode, string controller, string action, DateTime? startTime, DateTime? endTime);

        Task<AuditInfo> Details(int id);
    }
}
