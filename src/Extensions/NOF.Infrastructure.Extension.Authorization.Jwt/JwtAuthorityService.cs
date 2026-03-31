using NOF.Annotation;
using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = new[] { typeof(IJwtAuthorityService) })]
[ServiceImplementation<IJwtAuthorityService>]
public partial class JwtAuthorityService;
