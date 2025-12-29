# LowlandTech.Foundry.ServiceDefaults

The Aspire service defaults - provides consistent observability, health checks, and resilience patterns across all services.

## What This Project Does

ServiceDefaults configures cross-cutting concerns for all services:

- **OpenTelemetry**: Distributed tracing, metrics, and logging export
- **Health Checks**: Standardized health endpoints for orchestration
- **Service Discovery**: Integration with Aspire's service discovery
- **Resilience**: HTTP client retry policies and circuit breakers

## Why It's Standalone

**This project follows the standard Aspire pattern.** Here's the reasoning:

1. **Aspire Convention**: When you create an Aspire solution, it generates a ServiceDefaults project. This is the expected structure that other Aspire developers will recognize.

2. **Shared Infrastructure**: Both the Host and API reference this. It ensures consistent observability configuration without duplicating code.

3. **No Business Logic**: This project contains only infrastructure configuration. It doesn't know about plugins, P2P, or your domain. That's intentional.

4. **Easy Updates**: When Aspire releases new versions with better defaults, you update one project and all services benefit.

## Template Customization

When using this repo as a template:

- Add additional OpenTelemetry exporters (Jaeger, Zipkin, Azure Monitor)
- Configure custom health checks for your dependencies
- Adjust resilience policies for your use case
- This is usually "set and forget" once configured

## Typical Extension Method

```csharp
public static IHostApplicationBuilder AddServiceDefaults(this IHostApplicationBuilder builder)
{
    builder.ConfigureOpenTelemetry();
    builder.AddDefaultHealthChecks();
    builder.Services.AddServiceDiscovery();
    builder.Services.ConfigureHttpClientDefaults(http =>
    {
        http.AddStandardResilienceHandler();
        http.AddServiceDiscovery();
    });
    return builder;
}
```

## Dependencies

- `Microsoft.Extensions.Http.Resilience` - Polly-based resilience
- `Microsoft.Extensions.ServiceDiscovery` - Aspire service discovery
- `OpenTelemetry.*` packages - Observability

## Should You Merge It?

**Debatable, but probably not.**

Arguments for keeping it separate:
- Follows Aspire conventions
- Easy to update when Aspire evolves
- Clear separation of infrastructure vs application code

Arguments for merging into a "Core" project:
- It's a small project with one or two files
- If you're not using multiple Aspire services, the separation adds overhead

**Recommendation**: If you're keeping Aspire, keep this project. If you're removing Aspire, delete it and inline the OpenTelemetry config where needed.
