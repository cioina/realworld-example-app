using System.Collections.Generic;
using System.Security.Claims;
using Conduit.Features.Profiles;
using Conduit.Test.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Net.Http.Headers;
using MyTested.AspNetCore.Mvc;
using MyTested.AspNetCore.Mvc.Internal.Services;
using Xunit;

namespace Conduit.Test.Routing;

public class ControllerExceptionMessages
{
    //In browser, it returns 401
    public const string HeaderAuthorizationException =
@"Expected route '{0}' to match {1} action in {2} but action could not be invoked because 
of the declared filters - ApiControllerAttribute (Controller), AuthorizeFilter (Action), 
UnsupportedContentTypeFilter (Global). Either a filter is setting the response result before the 
action itself, or you must set the request properties so that they will pass through the pipeline.";
    //In browser, it returns 422 Queried object tag was not found, Key: 5
    public const string FromNotFoundException =
@"When calling {0} action in {1} expected no exception but AggregateException (containing 
{2} with 'Queried object {3} was not found, Key: {4}' message) was thrown without being caught.";
    //In browser, it returns 422
    public const string DifferenceException =
@"Expected route '{0}' to contain route value with '{1}' key and the provided value but 
the value was different. Difference occurs at '{2}'.";
    //In browser, it returns 422
    public const string DifferenceExtendedException =
@"Expected route '{0}' to contain route value with '{1}' key and the provided value but 
the value was different. Difference occurs at '{2}'. 
Expected a value of '{3}', but in fact it was '{4}'.";
    //In browser, it returns 422 Queried object tag was not found, Key: 0
    public const string DifferenceFormatException =
@"Expected route '{0}' to contain route value with '{1}' key and the provided value but 
the value was different - {2}.";
    //In browser, it returns 404
    public const string RouteCouldNotBeMachedException =
@"Expected route '{0}' to match {1} action in {2} but action could not be matched.";
    //In browser, it returns 422 Cannot find user profile.
    public const string HttpRequestException =
@"When building HttpRequest expected location to be URI valid, but instead received '{0}'.";
}

public class IdentityControllerRouteTest
{

    [Theory]
    [InlineData("userName", "admin@pixel.com", "Admi9@pixel")]
    public void Test_simple_route_should_success(
     string userName,
     string email,
     string password)
    {
        MyMvc
        .Pipeline()
        .ShouldMap(request => request
          .WithMethod(HttpMethod.Get)
          .WithLocation($"/api/profiles/{userName}1")
        )
        .To<ProfilesController>(c => c.Get($"{userName}1", TestServiceProvider.Current.GetService<IHttpContextAccessor>()!.HttpContext!.RequestAborted
        ).AsTask())
        .Which(controller => controller
           .WithData(db => db
             .WithEntities(entities => StaticTestData.GetUsersWithRole(
                 count: 3,
                 email: email,
                 userName: userName,
                 password: password,
                 dbContext: entities))));
    }
}
