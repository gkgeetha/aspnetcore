// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
using Microsoft.AspNetCore.Routing.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.ObjectPool;
using Microsoft.Extensions.Primitives;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Testing;

namespace Microsoft.AspNetCore.Http.Generators.Tests;

public abstract partial class RequestDelegateCreationTests
{
    [Theory]
    [InlineData(@"app.MapGet(""/hello"", () => ""Hello world!"");", "MapGet", "Hello world!")]
    [InlineData(@"app.MapPost(""/hello"", () => ""Hello world!"");", "MapPost", "Hello world!")]
    [InlineData(@"app.MapDelete(""/hello"", () => ""Hello world!"");", "MapDelete", "Hello world!")]
    [InlineData(@"app.MapPut(""/hello"", () => ""Hello world!"");", "MapPut", "Hello world!")]
    [InlineData(@"app.MapGet(pattern: ""/hello"", handler: () => ""Hello world!"");", "MapGet", "Hello world!")]
    [InlineData(@"app.MapPost(handler: () => ""Hello world!"", pattern: ""/hello"");", "MapPost", "Hello world!")]
    [InlineData(@"app.MapDelete(pattern: ""/hello"", handler: () => ""Hello world!"");", "MapDelete", "Hello world!")]
    [InlineData(@"app.MapPut(handler: () => ""Hello world!"", pattern: ""/hello"");", "MapPut", "Hello world!")]
    public async Task MapAction_NoParam_StringReturn(string source, string httpMethod, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, (endpointModel) =>
        {
            Assert.Equal(httpMethod, endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Fact]
    public async Task MapAction_NoParam_StringReturn_WithFilter()
    {
        var source = """
app.MapGet("/hello", () => "Hello world!")
    .AddEndpointFilter(async (context, next) => {
        var result = await next(context);
        return $"Filtered: {result}";
    });
""";
        var expectedBody = "Filtered: Hello world!";
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        await VerifyAgainstBaselineUsingFile(compilation);
        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Theory]
    [InlineData(@"app.MapGet(""/"", () => 123456);", "123456")]
    [InlineData(@"app.MapGet(""/"", () => true);", "true")]
    [InlineData(@"app.MapGet(""/"", () => new DateTime(2023, 1, 1));", @"""2023-01-01T00:00:00""")]
    public async Task MapAction_NoParam_AnyReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_ComplexReturn_Data => new List<object[]>()
    {
        new object[] { """app.MapGet("/", () => new Todo() { Name = "Test Item"});""" },
        new object[] { """
object GetTodo() => new Todo() { Name = "Test Item"};
app.MapGet("/", GetTodo);
"""},
        new object[] { """app.MapGet("/", () => TypedResults.Ok(new Todo() { Name = "Test Item"}));""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_ComplexReturn_Data))]
    public async Task MapAction_NoParam_ComplexReturn(string source)
    {
        var expectedBody = """{"id":0,"name":"Test Item","isComplete":false}""";
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_ExtensionResult_Data => new List<object[]>()
    {
        new object[] { """app.MapGet("/", () => Results.Extensions.TestResult());""" },
        new object[] { """app.MapGet("/", () => TypedResults.Extensions.TestResult());""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_ExtensionResult_Data))]
    public async Task MapAction_NoParam_ExtensionResult(string source)
    {
        var expectedBody = """Hello World!""";
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]>  MapAction_NoParam_TaskOfTReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(new Todo() { Name = ""Test Item"" }));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item"" })));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_TaskOfTReturn_Data))]
    public async Task MapAction_NoParam_TaskOfTReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
            Assert.True(endpointModel.Response.IsAwaitable);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_ValueTaskOfTReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => ValueTask.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_ValueTaskOfTReturn_Data))]
    public async Task MapAction_NoParam_ValueTaskOfTReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
            Assert.True(endpointModel.Response.IsAwaitable);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    public static IEnumerable<object[]> MapAction_NoParam_TaskLikeOfObjectReturn_Data => new List<object[]>()
    {
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(""Hello world!""));", "Hello world!" },
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(new Todo() { Name = ""Test Item""}));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => new ValueTask<object>(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" },
        new object[] { @"app.MapGet(""/"", () => Task<object>.FromResult(TypedResults.Ok(new Todo() { Name = ""Test Item""})));", """{"id":0,"name":"Test Item","isComplete":false}""" }
    };

    [Theory]
    [MemberData(nameof(MapAction_NoParam_TaskLikeOfObjectReturn_Data))]
    public async Task MapAction_NoParam_TaskLikeOfObjectReturn(string source, string expectedBody)
    {
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        VerifyStaticEndpointModel(result, endpointModel =>
        {
            Assert.Equal("MapGet", endpointModel.HttpMethod);
            Assert.True(endpointModel.Response.IsAwaitable);
        });

        var httpContext = CreateHttpContext();
        await endpoint.RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Fact]
    public async Task MapAction_HandlesCompletedTaskReturn()
    {
        var source = """
app.MapGet("/task", () => Task.CompletedTask);
app.MapGet("/value-task", () => ValueTask.CompletedTask);
""";
        var (result, compilation) = await RunGeneratorAsync(source);
        var endpoints = GetEndpointsFromCompilation(compilation);

        VerifyStaticEndpointModels(result, endpointModels => Assert.Collection(endpointModels,
            endpointModel =>
            {
                Assert.Equal("MapGet", endpointModel.HttpMethod);
                Assert.True(endpointModel.Response.IsAwaitable);
                Assert.True(endpointModel.Response.HasNoResponse);
            },
            endpointModel =>
            {
                Assert.Equal("MapGet", endpointModel.HttpMethod);
                Assert.True(endpointModel.Response.IsAwaitable);
                Assert.True(endpointModel.Response.HasNoResponse);
            }));

        var httpContext = CreateHttpContext();
        await endpoints[0].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, string.Empty);

        httpContext = CreateHttpContext();
        await endpoints[1].RequestDelegate(httpContext);
        await VerifyResponseBodyAsync(httpContext, string.Empty);
    }

    public static IEnumerable<object[]> JsonContextActions
    {
        get
        {
            yield return new[] { "TestAction", """Todo TestAction() => new Todo { Name = "Write even more tests!" };""" };
            yield return new[] { "TaskTestAction", """Task<Todo> TaskTestAction() => Task.FromResult(new Todo { Name = "Write even more tests!" });""" };
            yield return new[] { "ValueTaskTestAction", """ValueTask<Todo> ValueTaskTestAction() => ValueTask.FromResult(new Todo { Name = "Write even more tests!" });""" };

            yield return new[] { "StaticTestAction", """static Todo StaticTestAction() => new Todo { Name = "Write even more tests!" };""" };
            yield return new[] { "StaticTaskTestAction", """static Task<Todo> StaticTaskTestAction() => Task.FromResult(new Todo { Name = "Write even more tests!" });""" };
            yield return new[] { "StaticValueTaskTestAction", """static ValueTask<Todo> StaticValueTaskTestAction() => ValueTask.FromResult(new Todo { Name = "Write even more tests!" });""" };

            yield return new[] { "TestAction", """Todo TestAction() => new JsonTodoChild { Name = "Write even more tests!", Child = "With type hierarchies!" };""" };

            yield return new[] { "TaskTestAction", """Task<Todo> TaskTestAction() => Task.FromResult<Todo>(new JsonTodoChild { Name = "Write even more tests!", Child = "With type hierarchies!" });""" };
            yield return new[] { "TaskTestActionAwaited", """
                    async Task<Todo> TaskTestActionAwaited()
                    {
                        await Task.Yield();
                        return new JsonTodoChild { Name = "Write even more tests!", Child = "With type hierarchies!" };
                    }
                    """ };

            yield return new[] { "ValueTaskTestAction", """ValueTask<Todo> ValueTaskTestAction() => ValueTask.FromResult<Todo>(new JsonTodoChild { Name = "Write even more tests!", Child = "With type hierarchies!" });""" };
            yield return new[] { "ValueTaskTestActionAwaited", """
                    async ValueTask<Todo> ValueTaskTestActionAwaited()
                    {
                        await Task.Yield();
                        return new JsonTodoChild { Name = "Write even more tests!", Child = "With type hierarchies!" };
                    }
                    """ };
        }
    }

    [ConditionalTheory(Skip = "https://github.com/dotnet/runtime/issues/87073")]
    [MemberData(nameof(JsonContextActions))]
    public async Task RequestDelegateWritesAsJsonResponseBody_WithJsonSerializerContext(string delegateName, string delegateSource)
    {
        var source = $"""
app.MapGet("/test", {delegateName});

{delegateSource}
""";

        var (_, compilation) = await RunGeneratorAsync(source);
        var serviceProvider = CreateServiceProvider(serviceCollection =>
        {
            serviceCollection.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolver = SharedTestJsonContext.Default);
        });
        var endpoint = GetEndpointFromCompilation(compilation, serviceProvider: serviceProvider);

        var httpContext = CreateHttpContext(serviceProvider);

        await endpoint.RequestDelegate(httpContext);

        var deserializedResponseBody = JsonSerializer.Deserialize<Todo>(((MemoryStream)httpContext.Response.Body).ToArray(), new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });

        Assert.NotNull(deserializedResponseBody);
        Assert.Equal("Write even more tests!", deserializedResponseBody!.Name);
    }

    [ConditionalTheory(Skip = "https://github.com/dotnet/runtime/issues/87073")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestDelegateWritesAsJsonResponseBody_UnspeakableType(bool useJsonContext)
    {
        var source = """
app.MapGet("/todos", () => GetTodosAsync());

static async IAsyncEnumerable<JsonTodo> GetTodosAsync()
{
    yield return new JsonTodo() { Id = 1, IsComplete = true, Name = "One" };

    // ensure this is async
    await Task.Yield();

    yield return new JsonTodoChild() { Id = 2, IsComplete = false, Name = "Two", Child = "TwoChild" };
}
""";
        var (_, compilation) = await RunGeneratorAsync(source);
        var serviceProvider = CreateServiceProvider(serviceCollection =>
        {
            if (useJsonContext)
            {
                serviceCollection.ConfigureHttpJsonOptions(o =>
                {
                    o.SerializerOptions.TypeInfoResolverChain.Insert(0, SharedTestJsonContext.Default);
                    o.SerializerOptions.TypeInfoResolver = SharedTestJsonContext.Default;
                });
            }
        });
        var endpoint = GetEndpointFromCompilation(compilation, serviceProvider: serviceProvider);

        var httpContext = CreateHttpContext(serviceProvider);

        await endpoint.RequestDelegate(httpContext);

        var expectedBody = """[{"id":1,"name":"One","isComplete":true},{"$type":"JsonTodoChild","child":"TwoChild","id":2,"name":"Two","isComplete":false}]""";
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [ConditionalTheory(Skip = "https://github.com/dotnet/runtime/issues/87073")]
    [InlineData(true)]
    [InlineData(false)]
    public async Task RequestDelegateWritesAsJsonResponseBody_UnspeakableType_InFilter(bool useJsonContext)
    {
        var source = """
app.MapGet("/todos", () => "not going to be returned")
.AddEndpointFilterFactory((routeHandlerContext, next) => async (context) =>
{
    var result = await next(context);
    return new Todo { Name = "Write even more tests!" };
});
""";
        var (_, compilation) = await RunGeneratorAsync(source);
        var serviceProvider = CreateServiceProvider(serviceCollection =>
        {
            if (useJsonContext)
            {
                serviceCollection.ConfigureHttpJsonOptions(o => o.SerializerOptions.TypeInfoResolver = SharedTestJsonContext.Default);
            }
        });
        var endpoint = GetEndpointFromCompilation(compilation, serviceProvider: serviceProvider);

        var httpContext = CreateHttpContext(serviceProvider);

        await endpoint.RequestDelegate(httpContext);

        var expectedBody = """[{"id":1,"name":"One","isComplete":true},{"$type":"JsonTodoChild","child":"TwoChild","id":2,"name":"Two","isComplete":false}]""";
        await VerifyResponseBodyAsync(httpContext, expectedBody);
    }

    [Fact]
    public async Task SupportsIResultWithExplicitInterfaceImplementation()
    {
        var source = """
app.MapPost("/", () => new Status410Result());
""";
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();

        await endpoint.RequestDelegate(httpContext);

        await VerifyResponseBodyAsync(httpContext, "Already gone!", StatusCodes.Status410Gone);
    }

    public static IEnumerable<object[]> ComplexResult
    {
        get
        {
            var testAction = """
app.MapPost("/", () => new Todo() { Name = "Write even more tests!" });
""";

            var taskTestAction = """
app.MapPost("/", () => Task.FromResult(new Todo() { Name = "Write even more tests!" }));
""";

            var valueTaskTestAction = """
app.MapPost("/", () => ValueTask.FromResult(new Todo() { Name = "Write even more tests!" }));
""";

            var staticTestAction = """
app.MapPost("/", StaticTestAction);
static Todo StaticTestAction() => new Todo() { Name = "Write even more tests!" };
""";

            var staticTaskTestAction = """
app.MapPost("/", StaticTaskTestAction);
static Task<Todo> StaticTaskTestAction() => Task.FromResult(new Todo() { Name = "Write even more tests!" });
""";

            var staticValueTaskTestAction = """
app.MapPost("/", StaticValueTaskTestAction);
static ValueTask<Todo> StaticValueTaskTestAction() => ValueTask.FromResult(new Todo() { Name = "Write even more tests!" });
""";

            return new List<object[]>
                {
                    new object[] { testAction },
                    new object[] { taskTestAction },
                    new object[] { valueTaskTestAction },
                    new object[] { staticTestAction },
                    new object[] { staticTaskTestAction },
                    new object[] { staticValueTaskTestAction }
                };
        }
    }

    [Theory]
    [MemberData(nameof(ComplexResult))]
    public async Task RequestDelegateWritesComplexReturnValueAsJsonResponseBody(string source)
    {
        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();

        await endpoint.RequestDelegate(httpContext);

        await VerifyResponseJsonBodyAsync<Todo>(httpContext, (todo) =>
        {
            Assert.NotNull(todo);
            Assert.Equal("Write even more tests!", todo!.Name);
        });
    }

    [Fact]
    public async Task RequestDelegateWritesComplexStructReturnValueAsJsonResponseBody()
    {
        var source = """
app.MapPost("/", () => new TodoStruct(42, "Bob", true, TodoStatus.Done));
""";

        var (_, compilation) = await RunGeneratorAsync(source);
        var endpoint = GetEndpointFromCompilation(compilation);

        var httpContext = CreateHttpContext();

        await endpoint.RequestDelegate(httpContext);

        await VerifyResponseJsonBodyAsync<TodoStruct>(httpContext, (todo) =>
        {
            Assert.Equal(42, todo.Id);
            Assert.Equal("Bob", todo.Name);
            Assert.True(todo.IsComplete);
            Assert.Equal(TodoStatus.Done, todo.Status);
        });
    }
}
