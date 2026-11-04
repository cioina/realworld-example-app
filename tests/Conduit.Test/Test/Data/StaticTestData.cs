using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading;
using System.Threading.Tasks;
using Conduit.Domain;
using Conduit.Infrastructure;
using Conduit.Infrastructure.Security;
using Mediator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;
using MyTested.AspNetCore.Mvc.Internal.Services;

namespace Conduit.Test.Data;

public static class StaticTestData
{
    private class MiddlewareResult
    {
        public const int NoResult = 0;
        public const int GoodResult = 1;
        public const int RateLimitMiddlewareException = -1;
        public const int SecurityTokenRefreshException = -2;
        public const int MatchingRulesException = -3;
    }
    //private class Worker
    //{
    //    private volatile bool _shouldStop;
    //    public EventWaitHandle? WaitHandleExternal;

    //    public void DoWork()
    //    {
    //        while (!_shouldStop)
    //        {
    //            Thread.Sleep(2000);
    //            WaitHandleExternal!.Set();
    //        }
    //    }
    //    public void RequestStop()
    //    {
    //        _shouldStop = true;
    //    }
    //}
    public static IEnumerable<object> GetUsersWithRole(
    int count,
    string email,
    string userName,
    string password,
    DbContext dbContext)
    {
        var httpContext = TestServiceProvider.Current.GetService<IHttpContextAccessor>()!.HttpContext!;
        var passwordHasher = TestServiceProvider.Current.GetService<IPasswordHasher>();
        var db = TestServiceProvider.Current.GetService<ConduitContext>();

        var users = Enumerable
             .Range(1, count)
             .Select(async i =>
             {
                 var person = new Person
                 {
                     Username = userName,
                     Email = email,
                     Hash = await passwordHasher!.Hash(password, []),
                     Salt = []
                 };

                 return person;

             }).ToList();

        dbContext.AddRange(users);
        dbContext.SaveChanges();

        return Enumerable
         .Range(1, count)
         .Select(async i =>
         {
             var user = await db!.Persons.FirstAsync(
                 x => x.PersonId == i,
                 httpContext.RequestAborted);
             return user;
         }).ToList();
    }

}
