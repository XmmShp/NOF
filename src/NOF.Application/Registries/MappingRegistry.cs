using NOF.Abstraction;

namespace NOF.Application;

/// <summary>
/// Stores expression-based mapping registrations until application composition is complete.
/// </summary>
public sealed class MappingRegistry : Registry<MappingRegistration>;
