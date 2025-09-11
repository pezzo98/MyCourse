using System;
using System.IO;
using AspNetCore.ReCaptcha;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using MyCourse.Customizations.Identity;
using MyCourse.Customizations.ModelBinders;
using MyCourse.Models.Authorization;
using MyCourse.Models.Entities;
using MyCourse.Models.Enums;
using MyCourse.Models.Options;
using MyCourse.Models.Services.Application.Courses;
using MyCourse.Models.Services.Application.Lessons;
using MyCourse.Models.Services.Infrastructure;

namespace MyCourse
{
    public class Program()
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            builder.Services.AddTransient<IPaymentGateway, PaypalPaymentGateway>();
            // builder.Services.AddTransient<IPaymentGateway, StripePaymentGateway>();

            builder.Services.AddReCaptcha(builder.Configuration.GetSection("ReCaptcha"));
            builder.Services.AddResponseCaching();

            builder.Services.AddControllersWithViews(options =>
            {
                CacheProfile homeProfile = new();
                builder.Configuration.Bind("ResponseCache:Home", homeProfile);
                options.CacheProfiles.Add("Home", homeProfile);
                options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());
            });

            builder.Services.AddRazorPages(options =>
            {
                options.Conventions.AllowAnonymousToPage("/Privacy");
            });

            var identityBuilder = builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;
                options.SignIn.RequireConfirmedAccount = true;
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
            })
            .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>()
            .AddPasswordValidator<CommonPasswordValidator<ApplicationUser>>();

            var persistence = Persistence.AdoNet;
            switch (persistence)
            {
                case Persistence.AdoNet:
                    builder.Services.AddTransient<ICourseService, AdoNetCourseService>();
                    builder.Services.AddTransient<ILessonService, AdoNetLessonService>();
                    builder.Services.AddTransient<IDatabaseAccessor, SqliteDatabaseAccessor>();
                    identityBuilder.AddUserStore<AdoNetUserStore>();
                    break;

                case Persistence.EfCore:
                    identityBuilder.AddEntityFrameworkStores<MyCourseDbContext>();
                    builder.Services.AddTransient<ICourseService, EfCoreCourseService>();
                    builder.Services.AddTransient<ILessonService, EfCoreLessonService>();
                    builder.Services.AddDbContextPool<MyCourseDbContext>(options =>
                    {
                        string connectionString = builder.Configuration.GetConnectionString("Default");
                        options.UseSqlite(connectionString);
                    });
                    break;
            }

            builder.Services.AddTransient<ICachedCourseService, MemoryCacheCourseService>();
            builder.Services.AddTransient<ICachedLessonService, MemoryCacheLessonService>();
            builder.Services.AddSingleton<IImagePersister, MagickNetImagePersister>();
            builder.Services.AddSingleton<IEmailSender, MailKitEmailSender>();
            builder.Services.AddSingleton<IEmailClient, MailKitEmailSender>();
            builder.Services.AddSingleton<IAuthorizationPolicyProvider, MultiAuthorizationPolicyProvider>();
            builder.Services.AddSingleton<ITransactionLogger, LocalTransactionLogger>();

            builder.Services.AddScoped<IAuthorizationHandler, CourseAuthorRequirementHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, CourseSubscriberRequirementHandler>();
            builder.Services.AddScoped<IAuthorizationHandler, CourseLimitRequirementHandler>();

            builder.Services.AddAuthorizationBuilder()
                .AddPolicy(nameof(Policy.CourseAuthor), policy =>
                    policy.Requirements.Add(new CourseAuthorRequirement()))
                .AddPolicy(nameof(Policy.CourseSubscriber), policy =>
                    policy.Requirements.Add(new CourseSubscriberRequirement()))
                .AddPolicy(nameof(Policy.CourseLimit), policy =>
                    policy.Requirements.Add(new CourseLimitRequirement(limit: 5)));

            builder.Services.Configure<CoursesOptions>(builder.Configuration.GetSection("Courses"));
            builder.Services.Configure<ConnectionStringsOptions>(builder.Configuration.GetSection("ConnectionStrings"));
            builder.Services.Configure<MemoryCacheOptions>(builder.Configuration.GetSection("MemoryCache"));
            builder.Services.Configure<KestrelServerOptions>(builder.Configuration.GetSection("Kestrel"));
            builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection("Smtp"));
            builder.Services.Configure<UsersOptions>(builder.Configuration.GetSection("Users"));
            builder.Services.Configure<PaypalOptions>(builder.Configuration.GetSection("Paypal"));
            builder.Services.Configure<StripeOptions>(builder.Configuration.GetSection("Stripe"));

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.Lifetime.ApplicationStarted.Register(() =>
                {
                    string filePath = Path.Combine(app.Environment.ContentRootPath, "bin/reload.txt");
                    File.WriteAllText(filePath, DateTime.Now.ToString());
                });
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();
            app.UseStaticFiles();

            app.Use(async (context, next) =>
            {
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";
                context.Response.Headers["X-XSS-Protection"] = "1; mode=block";
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                await next();
            });

            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseResponseCaching();

            app.MapControllerRoute(name: "default", pattern: "{controller=Home}/{action=Index}/{id?}").RequireAuthorization();

            app.MapRazorPages().RequireAuthorization();

            app.Run();
        }
    }
}