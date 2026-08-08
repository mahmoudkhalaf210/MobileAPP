using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Snap.API.Errors;
using Snap.Application.Domain.Entities;
using Snap.Application.Identity.Interfaces;
using Snap.Application.Users.DTOs;
using Snap.Application.Users.Interfaces;
using System.Collections.Concurrent;
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

using System.Security.Claims;

namespace Snap.API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UsersIdentityController : ControllerBase
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly ITokenService _tokenService;
        private readonly IFcmTokenService _fcmTokenService;
        private readonly IConfiguration _configuration;
        private readonly IPasswordHasher<User> _passwordHasher;
        private static readonly ConcurrentDictionary<string, (string Otp, DateTime Expiry, bool Verified)> _otpStore = new();
        private static readonly TimeSpan OtpLifetime = TimeSpan.FromMinutes(5);

        public UsersIdentityController(UserManager<User>userManager ,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole> roleManager,
            ITokenService tokenService,
            IFcmTokenService fcmTokenService,
            IConfiguration configuration,
            IPasswordHasher<User> passwordHasher)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _roleManager = roleManager;
            _tokenService = tokenService;
            _fcmTokenService = fcmTokenService;
            _configuration = configuration;
            _passwordHasher = passwordHasher;
        }

        [HttpPost("SendOtp")]
        public ActionResult SendOtp([FromBody] SendOtpDto dto)
        {
            try
            {
                var otp = new Random().Next(100000, 999999).ToString();
                _otpStore[dto.PhoneNumber] = (otp, DateTime.UtcNow.Add(OtpLifetime), false);

                // WhatsApp sending logic
                var accountSid = _configuration["Twilio:AccountSid"];
                var authToken = _configuration["Twilio:AuthToken"];
                var fromNumber = _configuration["Twilio:WhatsAppFrom"];
                if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(fromNumber))
                {
                    TwilioClient.Init(accountSid, authToken);
                    var to = new PhoneNumber($"whatsapp:+2{dto.PhoneNumber}"); // Egypt country code as example
                    var from = new PhoneNumber(fromNumber);
                    var message = MessageResource.Create(
                        to: to,
                        from: from,
                        body: $"Your OTP is: {otp}"
                    );
                }
                return Ok(new { message = $"OTP sent to WhatsApp for {dto.PhoneNumber}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while sending OTP: {ex.Message}"));
            }
        }

        [HttpPost("VerifyOtp")]
        public ActionResult VerifyOtp([FromBody] VerifyOtpDto dto)
        {
            try
            {
                if (!_otpStore.TryGetValue(dto.PhoneNumber, out var entry))
                    return BadRequest(new ApiResponse(400, "OTP not requested or expired."));
                if (entry.Expiry < DateTime.UtcNow)
                    return BadRequest(new ApiResponse(400, "OTP expired."));
                if (entry.Otp != dto.Otp)
                    return BadRequest(new ApiResponse(400, "Invalid OTP."));
                _otpStore[dto.PhoneNumber] = (entry.Otp, entry.Expiry, true);
                return Ok(new { message = "OTP verified." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while verifying OTP: {ex.Message}"));
            }
        }

        //Register
        [HttpPost("Register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto model)
        {
            try
            {
                //if (!_otpStore.TryGetValue(model.PhoneNumber, out var entry) || !entry.Verified)
                //{
                //    return BadRequest(new ApiResponse(400, "Phone number not verified by OTP."));
                //}
                var existingUserByEmail = await _userManager.FindByEmailAsync(model.Email);
                if (existingUserByEmail != null)
                {
                    return BadRequest(new ApiResponse(400, "An account is already registered with this email."));
                }
                if (model.Gender.ToLower() != "male" && model.Gender.ToLower() != "female") {
                    return BadRequest(new ApiResponse(400, "Gender must be male or female only."));
                }

                var user = new User()
                {
                    FullName = model.FullName,
                    Email = model.Email,
                    UserName = model.Email.Split('@')[0],
                    PhoneNumber = model.PhoneNumber,
                    UserType = model.UserType,
                    Gender = model.Gender
                };
                var result = await _userManager.CreateAsync(user, model.password);
                if (!result.Succeeded) { return BadRequest(new ApiResponse(400 , "Password must be 6-20 characters, with at least 1 uppercase letter, 1 number, and 1 special character (e.g., _, -, @, $, etc.).\"\r\n")); }

                if (!await _roleManager.RoleExistsAsync(model.UserType))
                {
                    await _roleManager.CreateAsync(new IdentityRole(model.UserType));
                }
                await _userManager.AddToRoleAsync(user, model.UserType);

                var ReturnedUser = new UserDto()
                {
                    UserId = user.Id,
                    DispalyName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Token = await _tokenService.CreateTokenAsync(user , _userManager),
                    UserType = user.UserType,
                    Gender = user.Gender
                };
                _otpStore.TryRemove(model.PhoneNumber, out _);
                return Ok(ReturnedUser);
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while registering user: {ex.Message}"));
            }
        }
        //login
        [HttpPost("Login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto model)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(model.EmailOrPhone)
                           ?? await _userManager.Users.FirstOrDefaultAsync(u => u.PhoneNumber == model.EmailOrPhone);

                if (user == null)
                {
                    return Unauthorized(new ApiResponse(401, "Invalid email or phone number."));
                }

                var result = await _signInManager.CheckPasswordSignInAsync(user, model.Password, false);

                if (!result.Succeeded)
                {
                    return Unauthorized(new ApiResponse(401, "Invalid password."));
                }

                return Ok(new UserDto
                {
                    UserId = user.Id,
                    DispalyName = user.FullName,
                    Email = user.Email,
                    PhoneNumber = user.PhoneNumber,
                    Token = await _tokenService.CreateTokenAsync(user, _userManager),
                    UserType = user.UserType,
                    Gender = user.Gender
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while logging in: {ex.Message}"));
            }
        }


        [HttpDelete("Delete/{userId}")]
        public async Task<ActionResult> DeleteUser(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                {
                    return NotFound(new ApiResponse(404, "User not found."));
                }
                var result = await _userManager.DeleteAsync(user);
                if (!result.Succeeded)
                {
                    return BadRequest(new ApiResponse(400, "Failed to delete user."));
                }
                return Ok(new { message = "User deleted successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while deleting user: {ex.Message}"));
            }
        }

        // Request OTP for password reset
        [HttpPost("RequestResetPasswordOtp")]
        public ActionResult RequestResetPasswordOtp([FromBody] ResetPasswordRequestDto dto)
        {
            try
            {
                var user = _userManager.Users.FirstOrDefault(u => u.PhoneNumber == dto.PhoneNumber);
                if (user == null)
                    return BadRequest(new ApiResponse(400, "No user found with this phone number."));
                var otp = new Random().Next(100000, 999999).ToString();
                _otpStore[$"reset_{dto.PhoneNumber}"] = (otp, DateTime.UtcNow.Add(OtpLifetime), false);
                var accountSid = _configuration["Twilio:AccountSid"];
                var authToken = _configuration["Twilio:AuthToken"];
                var fromNumber = _configuration["Twilio:WhatsAppFrom"];
                if (!string.IsNullOrEmpty(accountSid) && !string.IsNullOrEmpty(authToken) && !string.IsNullOrEmpty(fromNumber))
                {
                    TwilioClient.Init(accountSid, authToken);
                    var to = new PhoneNumber($"whatsapp:+2{dto.PhoneNumber}");
                    var from = new PhoneNumber(fromNumber);
                    var message = MessageResource.Create(
                        to: to,
                        from: from,
                        body: $"Your password reset OTP is: {otp}"
                    );
                }
                return Ok(new { message = $"Password reset OTP sent to WhatsApp for {dto.PhoneNumber}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while requesting reset password OTP: {ex.Message}"));
            }
        }

        // Verify OTP for password reset
        [HttpPost("VerifyResetPasswordOtp")]
        public ActionResult VerifyResetPasswordOtp([FromBody] ResetPasswordVerifyOtpDto dto)
        {
            try
            {
                if (!_otpStore.TryGetValue($"reset_{dto.PhoneNumber}", out var entry))
                    return BadRequest(new ApiResponse(400, "OTP not requested or expired."));
                if (entry.Expiry < DateTime.UtcNow)
                    return BadRequest(new ApiResponse(400, "OTP expired."));
                if (entry.Otp != dto.Otp)
                    return BadRequest(new ApiResponse(400, "Invalid OTP."));
                _otpStore[$"reset_{dto.PhoneNumber}"] = (entry.Otp, entry.Expiry, true);
                return Ok(new { message = "OTP verified for password reset." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while verifying reset password OTP: {ex.Message}"));
            }
        }

        // Reset password
        [HttpPost("ResetPassword")]
        public async Task<ActionResult> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            try
            {
                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                    return BadRequest(new ApiResponse(400, "No user found with this email."));

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);

                if (!result.Succeeded)
                    return BadRequest(new ApiResponse(400, "Failed to reset password."));

                return Ok(new { message = "Password reset successful." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while resetting password: {ex.Message}"));
            }
        }

        // PUT: api/UsersIdentity/UpdateImage/{userId}
        [HttpPut("UpdateImage/{userId}")]
        public async Task<IActionResult> UpdateUserImage(string userId, [FromBody] string image)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));
                user.Image = image ?? string.Empty;
                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return BadRequest(new ApiResponse(400, "Failed to update user image."));
                return Ok(new { message = "User image updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating user image: {ex.Message}"));
            }
        }

        // GET: api/UsersIdentity/GetImage/{userId}
        [HttpGet("GetImage/{userId}")]
        public async Task<IActionResult> GetUserImage(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));
                return Ok(new { image = user.Image ?? string.Empty });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user image: {ex.Message}"));
            }
        }
        [HttpPost("save-fcm-token")]
        public async Task<IActionResult> SaveFCMToken([FromBody] FCMTokenDto dto)
        {
            try
            {
                if (string.IsNullOrEmpty(dto.Email))
                    return BadRequest(new ApiResponse(400, "Email is required."));

                var user = await _userManager.FindByEmailAsync(dto.Email);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));

                await _fcmTokenService.SaveTokenAsync(user.Id, dto.Token);
                return Ok(new { message = "FCM Token saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while saving FCM token: {ex.Message}"));
            }
        }

        [HttpGet("Profile/{userId}")]
        public async Task<ActionResult<UserProfileDto>> GetProfile(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));

                var claims = await _userManager.GetClaimsAsync(user);
                var emergencyName = claims.FirstOrDefault(c => c.Type == "emergency_contact_name")?.Value;
                var emergencyPhone = claims.FirstOrDefault(c => c.Type == "emergency_contact_phone")?.Value;

                return Ok(new UserProfileDto
                {
                    UserId = user.Id,
                    FullName = user.FullName,
                    PhoneNumber = user.PhoneNumber,
                    Email = user.Email,
                    Image = user.Image ?? string.Empty,
                    UserType = user.UserType,
                    Gender = user.Gender,
                    EmergencyContact = string.IsNullOrWhiteSpace(emergencyName) && string.IsNullOrWhiteSpace(emergencyPhone)
                        ? null
                        : new EmergencyContactDto { Name = emergencyName, PhoneNumber = emergencyPhone }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting user profile: {ex.Message}"));
            }
        }

        [HttpPut("UpdateProfile/{userId}")]
        public async Task<IActionResult> UpdateProfile(string userId, [FromBody] UpdateUserProfileDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new ApiResponse(400, "Invalid payload"));

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));

                if (!string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    var phoneInUse = await _userManager.Users.AnyAsync(u => u.PhoneNumber == dto.PhoneNumber && u.Id != userId);
                    if (phoneInUse)
                        return BadRequest(new ApiResponse(400, "Phone number is already used by another account."));
                    user.PhoneNumber = dto.PhoneNumber;
                }

                if (!string.IsNullOrWhiteSpace(dto.FullName))
                    user.FullName = dto.FullName;

                if (dto.Image != null)
                    user.Image = dto.Image;

                var result = await _userManager.UpdateAsync(user);
                if (!result.Succeeded)
                    return BadRequest(new ApiResponse(400, "Failed to update user profile."));

                return Ok(new { message = "User profile updated successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while updating user profile: {ex.Message}"));
            }
        }

        [HttpGet("EmergencyContact/{userId}")]
        public async Task<ActionResult<EmergencyContactDto>> GetEmergencyContact(string userId)
        {
            try
            {
                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));

                var claims = await _userManager.GetClaimsAsync(user);
                var emergencyName = claims.FirstOrDefault(c => c.Type == "emergency_contact_name")?.Value;
                var emergencyPhone = claims.FirstOrDefault(c => c.Type == "emergency_contact_phone")?.Value;

                return Ok(new EmergencyContactDto { Name = emergencyName, PhoneNumber = emergencyPhone });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while getting emergency contact: {ex.Message}"));
            }
        }

        [HttpPut("EmergencyContact/{userId}")]
        public async Task<IActionResult> UpsertEmergencyContact(string userId, [FromBody] EmergencyContactDto dto)
        {
            try
            {
                if (dto == null)
                    return BadRequest(new ApiResponse(400, "Invalid payload"));

                var user = await _userManager.FindByIdAsync(userId);
                if (user == null)
                    return NotFound(new ApiResponse(404, "User not found."));

                var claims = await _userManager.GetClaimsAsync(user);

                await UpsertOrRemoveClaim(user, claims, "emergency_contact_name", dto.Name);
                await UpsertOrRemoveClaim(user, claims, "emergency_contact_phone", dto.PhoneNumber);

                return Ok(new { message = "Emergency contact saved successfully." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse(500, $"An error occurred while saving emergency contact: {ex.Message}"));
            }
        }

        private async Task UpsertOrRemoveClaim(User user, IList<Claim> existingClaims, string claimType, string? value)
        {
            var current = existingClaims.FirstOrDefault(c => c.Type == claimType);

            if (string.IsNullOrWhiteSpace(value))
            {
                if (current != null)
                    await _userManager.RemoveClaimAsync(user, current);
                return;
            }

            if (current == null)
            {
                await _userManager.AddClaimAsync(user, new Claim(claimType, value));
                return;
            }

            if (!string.Equals(current.Value, value, StringComparison.Ordinal))
                await _userManager.ReplaceClaimAsync(user, current, new Claim(claimType, value));
        }
    }
}
