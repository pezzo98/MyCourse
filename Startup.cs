﻿using System;
using System.IO;
using AspNetCore.ReCaptcha;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
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
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }
        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        // For more information on how to configure your application, visit https://go.microsoft.com/fwlink/?LinkID=398940
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddReCaptcha(Configuration.GetSection("ReCaptcha"));
            services.AddResponseCaching();

            services.AddMvc(options =>
            {
                CacheProfile homeProfile = new();
                //homeProfile.Duration = Configuration.GetValue<int>("ResponseCache:Home:Duration");
                //homeProfile.Location = Configuration.GetValue<ResponseCacheLocation>("ResponseCache:Home:Location");
                //homeProfile.VaryByQueryKeys = new string[] { "page" };
                Configuration.Bind("ResponseCache:Home", homeProfile);
                options.CacheProfiles.Add("Home", homeProfile);

                options.ModelBinderProviders.Insert(0, new DecimalModelBinderProvider());

            });

            services.AddRazorPages(options =>
            {
                options.Conventions.AllowAnonymousToPage("/Privacy");
            });

            var identityBuilder = services.AddDefaultIdentity<ApplicationUser>(options =>
            {
                // Criteri di validazione della password
                options.Password.RequireDigit = true;
                options.Password.RequiredLength = 8;
                options.Password.RequireUppercase = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredUniqueChars = 4;

                // Conferma dell'account
                options.SignIn.RequireConfirmedAccount = true;

                // Blocco dell'account
                options.Lockout.AllowedForNewUsers = true;
                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);

            })
                    .AddClaimsPrincipalFactory<CustomClaimsPrincipalFactory>()
                    .AddPasswordValidator<CommonPasswordValidator<ApplicationUser>>();

            //Usiamo ADO.NET o Entity Framework Core per l'accesso ai dati?
            var persistence = Persistence.EfCore;
            switch (persistence)
            {
                case Persistence.AdoNet:
                    services.AddTransient<ICourseService, AdoNetCourseService>();
                    services.AddTransient<ILessonService, AdoNetLessonService>();
                    services.AddTransient<IDatabaseAccessor, SqliteDatabaseAccessor>();

                    //Imposta l'AdoNetUserStore come servizio di persistenza per Identity
                    identityBuilder.AddUserStore<AdoNetUserStore>();

                    break;

                case Persistence.EfCore:

                    //Imposta il MyCourseDbContext come servizio di persistenza per Identity
                    identityBuilder.AddEntityFrameworkStores<MyCourseDbContext>();

                    services.AddTransient<ICourseService, EfCoreCourseService>();
                    services.AddTransient<ILessonService, EfCoreLessonService>();

                    // Usando AddDbContextPool, il DbContext verrà implicitamente registrato con il ciclo di vita Scoped
                    services.AddDbContextPool<MyCourseDbContext>(optionsBuilder =>
                    {
                        string connectionString = Configuration.GetSection("ConnectionStrings").GetValue<string>("Default");
                        optionsBuilder.UseSqlite(connectionString);
                    });
                    break;
            }

            services.AddTransient<ICachedCourseService, MemoryCacheCourseService>();
            services.AddTransient<ICachedLessonService, MemoryCacheLessonService>();
            services.AddSingleton<IImagePersister, MagickNetImagePersister>();
            services.AddSingleton<IEmailSender, MailKitEmailSender>();
            services.AddSingleton<IEmailClient, MailKitEmailSender>();

            // Uso il ciclo di vita Scoped per registrare questi AuthorizationHandler perché
            // sfruttano un servizio (il DbContext) registrato con il ciclo di vita Scoped
            services.AddScoped<IAuthorizationHandler, CourseAuthorRequirementHandler>();
            services.AddScoped<IAuthorizationHandler, CourseLimitRequirementHandler>();

            // Policies
            services.AddAuthorization(options =>
            {
                options.AddPolicy(nameof(Policy.CourseAuthor), builder =>
                {
                    builder.Requirements.Add(new CourseAuthorRequirement());
                });

                options.AddPolicy(nameof(Policy.CourseLimit), builder =>
                {
                    builder.Requirements.Add(new CourseLimitRequirement(limit: 5));
                });
            });

            // Options
            services.Configure<CoursesOptions>(Configuration.GetSection("Courses"));
            services.Configure<ConnectionStringsOptions>(Configuration.GetSection("ConnectionStrings"));
            services.Configure<MemoryCacheOptions>(Configuration.GetSection("MemoryCache"));
            services.Configure<KestrelServerOptions>(Configuration.GetSection("Kestrel"));
            services.Configure<SmtpOptions>(Configuration.GetSection("Smtp"));
            services.Configure<UsersOptions>(Configuration.GetSection("Users"));
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env, IHostApplicationLifetime lifetime)
        {
            //if (env.IsDevelopment())
            if (env.IsEnvironment("Development"))
            {
                app.UseDeveloperExceptionPage();

                //Aggiorniamo un file per notificare al BrowserSync che deve aggiornare la pagina
                lifetime.ApplicationStarted.Register(() =>
                {
                    string filePath = Path.Combine(env.ContentRootPath, "bin/reload.txt");
                    File.WriteAllText(filePath, DateTime.Now.ToString());
                });
            }
            else
            {
                // app.UseExceptionHandler("/Error");
                // Breaking change .NET 5: https://docs.microsoft.com/en-us/dotnet/core/compatibility/aspnet-core/5.0/middleware-exception-handler-throws-original-exception
                app.UseExceptionHandler(new ExceptionHandlerOptions
                {
                    ExceptionHandlingPath = "/Error",
                    AllowStatusCode404Response = true
                });
            }

            app.UseStaticFiles();

            //Nel caso volessi impostare una Culture specifica...
            /*var appCulture = CultureInfo.InvariantCulture;
            app.UseRequestLocalization(new RequestLocalizationOptions
            {
                DefaultRequestCulture = new RequestCulture(appCulture),
                SupportedCultures = new[] { appCulture }
            });*/

            //EndpointRoutingMiddleware
            app.UseRouting();

            app.UseAuthentication();
            app.UseAuthorization();

            app.UseResponseCaching();

            //EndpointMiddleware
            app.UseEndpoints(routeBuilder =>
            {
                routeBuilder.MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}").RequireAuthorization();
                routeBuilder.MapRazorPages().RequireAuthorization();
            });
        }
    }
}
