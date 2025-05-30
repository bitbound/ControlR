using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Xunit;
using Xunit.Abstractions;
using System.Collections.Immutable;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;

namespace ControlR.Web.Server.Tests.Helpers;

/// <summary>
/// Extension methods to help with debugging EF Core queries
/// </summary>
public static class EntityFrameworkQueryHelper
{
    /// <summary>
    /// Converts database-level queries to client-side evaluation for tests
    /// </summary>
    /// <param name="query">The original query</param>
    /// <returns>A queryable that will be evaluated client-side</returns>
    public static IQueryable<T> AsClientEvaluation<T>(this IQueryable<T> query)
    {
        // This forces client-side evaluation for tests
        return query.ToList().AsQueryable();
    }
}
