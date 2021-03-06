﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Security.Claims;
using System.Threading.Tasks;
using DotVVM.Framework.Hosting;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

namespace DotVVM.Framework.Runtime.Filters
{
    /// <summary>
    /// Recognizes <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute" /> filters and applies a specific <see cref="AuthorizationPolicy" />.
    /// </summary>
    public class AuthorizeAttribute : ActionFilterAttribute
    {
        private static readonly ConcurrentDictionary<Type, bool> isAnonymousAllowedCache = new ConcurrentDictionary<Type, bool>();

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute" /> class.
        /// </summary>
        public AuthorizeAttribute()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="Microsoft.AspNetCore.Authorization.AuthorizeAttribute" /> class with the specified policy.
        /// </summary>
        /// <param name="policy">The name of the policy to require for authorization.</param>
        public AuthorizeAttribute(string policy)
        {
            Policy = policy;
        }

        /// <summary>
        /// Gets or sets the policy name that determines access to the resource.
        /// </summary>
        public string Policy { get; set; }

        /// <summary>
        /// Gets or sets a comma delimited list of roles that are allowed to access the resource.
        /// </summary>
        public string Roles { get; set; }

        /// <summary>
        /// Gets or sets a comma delimited list of schemes from which user information is constructed.
        /// </summary>
        public string ActiveAuthenticationSchemes { get; set; }
        

        /// <inheritdoc />
        protected override Task OnViewModelCreatedAsync(IDotvvmRequestContext context)
            => Authorize(context, context.ViewModel);

        /// <inheritdoc />
        protected override Task OnCommandExecutingAsync(IDotvvmRequestContext context, ActionInfo actionInfo)
            => Authorize(context, null);

        protected override Task OnPageLoadingAsync(IDotvvmRequestContext context)
            => Authorize(context, context.Presenter);

        private async Task Authorize(IDotvvmRequestContext context, object appliedOn)
        {
            var policy = await GetAuthorizationPolicy(context);

            if (policy == null)
            {
                return;
            }

            var coreContext = context.GetAspNetCoreContext();

            if (policy.AuthenticationSchemes != null && policy.AuthenticationSchemes.Count > 0)
            {
                ClaimsPrincipal principal = null;

                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    var result = await coreContext.Authentication.AuthenticateAsync(scheme);

                    if (result != null)
                    {
                        principal = MergeUserPrincipal(principal, result);
                    }
                }

                if (principal == null)
                {
                    principal = new ClaimsPrincipal(new ClaimsIdentity());
                }

                coreContext.User = principal;
            }

            if (IsAnonymousAllowed(appliedOn))
            {
                return;
            }

            var authService = coreContext.RequestServices.GetRequiredService<IAuthorizationService>();

            if (!await authService.AuthorizeAsync(coreContext.User, context, policy))
            {
                await HandleUnauthorizedRequest(coreContext, policy);
            }
        }

        private async Task<AuthorizationPolicy> GetAuthorizationPolicy(IDotvvmRequestContext context)
        {
            var policyProvider = GetPolicyProvider(context);

            var policyBuilder = new AuthorizationPolicyBuilder();
            var useDefaultPolicy = true;

            if (!string.IsNullOrWhiteSpace(Policy))
            {
                var policy = await policyProvider.GetPolicyAsync(Policy);
                if (policy == null)
                {
                    throw new InvalidOperationException($"The policy '{Policy}' could not be found!");
                }
                policyBuilder.Combine(policy);
                useDefaultPolicy = false;
            }
            var rolesSplit = Roles?.Split(',');
            if (rolesSplit != null && rolesSplit.Any())
            {
                var trimmedRolesSplit = rolesSplit.Where(r => !string.IsNullOrWhiteSpace(r)).Select(r => r.Trim());
                policyBuilder.RequireRole(trimmedRolesSplit);
                useDefaultPolicy = false;
            }
            var authTypesSplit = ActiveAuthenticationSchemes?.Split(',');
            if (authTypesSplit != null && authTypesSplit.Any())
            {
                foreach (var authType in authTypesSplit)
                {
                    if (!string.IsNullOrWhiteSpace(authType))
                    {
                        policyBuilder.AuthenticationSchemes.Add(authType.Trim());
                    }
                }
            }
            if (useDefaultPolicy)
            {
                policyBuilder.Combine(await policyProvider.GetDefaultPolicyAsync());
            }

            return policyBuilder.Build();
        }

        private IAuthorizationPolicyProvider GetPolicyProvider(IDotvvmRequestContext context)
            => context.GetAspNetCoreContext().RequestServices.GetRequiredService<IAuthorizationPolicyProvider>();

        private bool IsAnonymousAllowed(object viewModel)
            => viewModel != null && isAnonymousAllowedCache.GetOrAdd(viewModel.GetType(), t => t.GetTypeInfo().GetCustomAttributes().OfType<IAllowAnonymous>().Any());

        private async Task HandleUnauthorizedRequest(HttpContext context, AuthorizationPolicy policy)
        {
            if (policy.AuthenticationSchemes != null && policy.AuthenticationSchemes.Count > 0)
            {
                foreach (var scheme in policy.AuthenticationSchemes)
                {
                    await context.Authentication.ChallengeAsync(scheme);
                }
            }
            else
            {
                await context.Authentication.ChallengeAsync();
            }

            throw new DotvvmInterruptRequestExecutionException("User unauthorized");
        }

        private ClaimsPrincipal MergeUserPrincipal(ClaimsPrincipal existingPrincipal, ClaimsPrincipal additionalPrincipal)
        {
            var result = new ClaimsPrincipal();

            if (additionalPrincipal != null)
            {
                result.AddIdentities(additionalPrincipal.Identities);
            }

            if (existingPrincipal != null)
            {
                result.AddIdentities(existingPrincipal.Identities.Where(i => i.IsAuthenticated || i.Claims.Any()));
            }

            return result;
        }

        private IAuthorizeData WrapInAuthorizeAttribute(IAuthorizeData authorizeDatum)
        {
            // TODO: remove when fixed (see https://github.com/aspnet/Security/commit/651815c282bfc594762346f4445afd9e6b48bb1e)

            return new Microsoft.AspNetCore.Authorization.AuthorizeAttribute {
                Policy = authorizeDatum.Policy,
                Roles = authorizeDatum.Roles,
                ActiveAuthenticationSchemes = authorizeDatum.ActiveAuthenticationSchemes
            };
        }
    }
}