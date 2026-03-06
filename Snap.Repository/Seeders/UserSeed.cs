using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Snap.Core.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snap.Repository.Seeders
{
    public class UserSeed
    {
        public static async Task SeedUserAsync(UserManager<User> userManager, RoleManager<IdentityRole> roleManager)
        {
            // Seed Roles
            if (!await roleManager.Roles.AnyAsync())
            {
                var roles = new List<IdentityRole>
                {
                    new IdentityRole { Name = "driver" },
                    new IdentityRole { Name = "passenger" },
                    new IdentityRole { Name = "admin" }
                };

                foreach (var role in roles)
                {
                    await roleManager.CreateAsync(role);
                }
            }


            if (!await userManager.Users.AnyAsync())
            {

                var user1 = new User()
                {
                    FullName = "youssef_essam ",
                    UserName = "youssefessam",
                    Email = "youssefessam@gmail.com",
                    PhoneNumber = "1234567890",
                    UserType = "admin",
                    Gender = "male"
                };
                var result = await userManager.CreateAsync(user1, "Test1!");

                if (result.Succeeded)
                {
                    await userManager.AddToRoleAsync(user1, "admin");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        Console.WriteLine($"Error: {error.Description}");
                    }
                }

            }
        }
    }
}
