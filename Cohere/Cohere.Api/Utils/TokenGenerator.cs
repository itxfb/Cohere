using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

using Cohere.Api.Settings;
using Cohere.Api.Utils.Abstractions;
using Cohere.Domain.Models.Account;
using Cohere.Entity.Infrastructure.Options;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Cohere.Api.Utils
{
    public class TokenGenerator : ITokenGenerator
    {
        private readonly IOptions<SecretsSettings> _encryptionSettings;
        private readonly IOptions<JwtSettings> _jwtSettings;

        public TokenGenerator(IOptions<SecretsSettings> encryptionSettings, IOptions<JwtSettings> jwtSettings)
        {
            _encryptionSettings = encryptionSettings;
            _jwtSettings = jwtSettings;
        }

        public string GenerateToken(AccountViewModel accountVm)
        {
            var utcNow = DateTime.UtcNow;

            using var privateRsa = RSA.Create();
            privateRsa.FromXmlString(_encryptionSettings.Value.JwtRsaPrivateKeyXml);
            var privateKey = new RsaSecurityKey(privateRsa) { KeyId = _jwtSettings.Value.KeyId };
            var signingCredentials = new SigningCredentials(privateKey, SecurityAlgorithms.RsaSha256)
            {
                CryptoProviderFactory = new CryptoProviderFactory { CacheSignatureProviders = false }
            };

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, accountVm.Id),
                //new Claim(JwtRegisteredClaimNames.Email, accountVm.Email),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            };

            foreach (var role in accountVm.Roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));
            }

            var jwt = new JwtSecurityToken(
                signingCredentials: signingCredentials,
                claims: claims,
                notBefore: utcNow,
                expires: utcNow.AddSeconds(_jwtSettings.Value.LifetimeSeconds),
                audience: _jwtSettings.Value.Audience,
                issuer: _jwtSettings.Value.Issuer);

            return new JwtSecurityTokenHandler().WriteToken(jwt);
        }
    }
}
