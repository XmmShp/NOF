using NOF.Annotation;
using NOF.Application;

namespace NOF.Infrastructure.Extension.Authorization.Jwt;

[AutoInject(Lifetime.Scoped, RegisterTypes = [typeof(IJwtAuthorityService)])]
[ServiceImplementation<IJwtAuthorityService>]
public partial class JwtAuthorityService;
