namespace UserService.Api.GraphQL;

public static class GraphQLServiceCollectionExtensions
{
    // Registers the HotChocolate server. The schema is served at /graphql (see MapGraphQL in Program.cs).
    public static IServiceCollection AddGraphQLPresentation(this IServiceCollection services)
    {
        services
            .AddGraphQLServer()
            .AddAuthorization()
            .AddQueryType<UserProfileQueries>()
            .AddMutationType<UserProfileMutations>();

        return services;
    }
}
