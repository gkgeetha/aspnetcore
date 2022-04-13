// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Net;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Xunit;

namespace Microsoft.AspNetCore.Mvc.FunctionalTests
{
    public class VersioningTests : VersioningTestsBase<VersioningWebSite.StartupWithoutEndpointRouting>
    {
        public VersioningTests(MvcTestFixture<VersioningWebSite.StartupWithoutEndpointRouting> fixture)
            : base(fixture)
        {
        }

        [Fact]
        public override async Task HasEndpointMatch()
        {
            // Arrange & Act
            var response = await Client.GetAsync("http://localhost/Routing/HasEndpointMatch");

            // Assert
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);

            var body = await response.Content.ReadAsStringAsync();
            var result = JsonConvert.DeserializeObject<bool>(body);

            Assert.False(result);
        }
    }
}