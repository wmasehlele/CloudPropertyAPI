﻿using CloudProperty.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;

namespace CloudProperty.Controllers
{
    public class AppController : ControllerBase
    {
        protected int AuthUserID => int.Parse(FindClaim(ClaimTypes.NameIdentifier));
        protected DatabaseContext _context;
        protected IConfiguration _configuration;
        protected DataCache _dataCache;

        protected string FindClaim(string claimName)
        {
            var claimsIdentity = HttpContext.User.Identity as ClaimsIdentity;
            var claim = claimsIdentity.FindFirst(claimName);

            if (claim == null)
            {
                return null;
            }
            return claim.Value;
        }

        protected RefreshToken GenerateRefreshToken(User user)
        {
            var refreshToken = new RefreshToken
            {
                Token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64)),
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };
            return refreshToken;
        }

        protected async Task<bool> SetRefreshToken(RefreshToken newRefreshToken, User user)
        {
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Expires = newRefreshToken.ExpiresAt
            };
            Response.Cookies.Append("refreshToken", newRefreshToken.Token, cookieOptions);

            user.RefreshToken = newRefreshToken.Token;
            user.RefreshTokenCreatedAt = newRefreshToken.CreatedAt;
            user.RefreshTokenExpiresAt = newRefreshToken.ExpiresAt;
            user.UpdatedAt = newRefreshToken.CreatedAt;
            
            await _context.SaveChangesAsync();

            return true;
        }

        protected string CreateToken(User user)
        {
            List<Claim> claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Email),

                //new Claim(ClaimTypes.Role, "Hola")
            };
            var key = new SymmetricSecurityKey(System.Text.Encoding.UTF8.GetBytes(_configuration.GetSection("AppSettings:secrete").Value));
            var cred = new SigningCredentials(key, SecurityAlgorithms.HmacSha512Signature);
            var token = new JwtSecurityToken(
                claims: claims,
                expires: DateTime.UtcNow.AddHours(1),
                signingCredentials: cred);
            var jwt = new JwtSecurityTokenHandler().WriteToken(token);
            return jwt;
        }

        protected void CreatePasswordHash(string password, out string passwordHash)
        {
            string salt = _configuration.GetSection("AppSettings:secrete").Value;
            byte[] passwordSalt = System.Text.Encoding.UTF8.GetBytes(salt);
            using (var hmac = new HMACSHA512(passwordSalt))
            {
                byte[] computeHash = hmac.ComputeHash(System.Text.Encoding.UTF8.GetBytes(password));
                passwordHash = Convert.ToBase64String(computeHash);
            }
        }

        protected bool verifyPasswordHash(string password, string passwordHash)
        {
            CreatePasswordHash(password, out string computedHash);
            return passwordHash == computedHash;
        }
    }
}
