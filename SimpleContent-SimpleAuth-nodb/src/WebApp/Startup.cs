﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using cloudscribe.SimpleContent.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Http;

namespace WebApp
{
    public class Startup
    {
        public Startup(IHostingEnvironment env)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(env.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
                .AddJsonFile($"appsettings.{env.EnvironmentName}.json", optional: true)
                .AddJsonFile("simpleauth-settings.json", optional: true, reloadOnChange: true)
                .AddJsonFile("simplecontent-settings.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();
            Configuration = builder.Build();
        }

        public IConfigurationRoot Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // Add framework services.

            services.AddLocalization(options => options.ResourcesPath = "GlobalResources");

            ConfigureAuthPolicy(services);

            
            // add users and settings from simpleauth-settings.json
            services.Configure<cloudscribe.Web.SimpleAuth.Models.SimpleAuthSettings>(Configuration.GetSection("SimpleAuthSettings"));
            services.Configure<List<cloudscribe.Web.SimpleAuth.Models.SimpleAuthUser>>(Configuration.GetSection("Users"));

            services.AddCloudscribeSimpleAuth();

            services.AddScoped<cloudscribe.SimpleContent.Models.IProjectQueries, cloudscribe.SimpleContent.Storage.NoDb.ConfigProjectQueries>();
            services.AddNoDbStorageForSimpleContent();

            services.AddCloudscribeNavigation(Configuration.GetSection("NavigationOptions"));
            //add content project settings from simplecontent-settings.json
            services.Configure<List<ProjectSettings>>(Configuration.GetSection("ContentProjects"));

            // this implementation of IProjectSecurityResolver provides integration with SimpleAuth
            // to use a different authentication system you would implement and plugin your own IProjectSecurityResolver
            services.AddScoped<IProjectSecurityResolver, cloudscribe.SimpleContent.Security.SimpleAuth.ProjectSecurityResolver>();

            services.AddCloudscribeCommmon(Configuration);
            services.AddSimpleContent();
            services.AddMetaWeblogForSimpleContent(Configuration.GetSection("MetaWeblogApiOptions"));
            services.AddSimpleContentRssSyndiction();
            services.AddCloudscribeFileManager(Configuration);
            
            // Add MVC services to the services container.

            services.Configure<MvcOptions>(options =>
            {
                options.CacheProfiles.Add("SiteMapCacheProfile",
                     new CacheProfile
                     {
                         Duration = 30
                     });

                options.CacheProfiles.Add("RssCacheProfile",
                     new CacheProfile
                     {
                         Duration = 100
                     });

            });

            services.AddRouting(options =>
            {
                options.LowercaseUrls = true;
            });

            services.AddMvc()
                .AddRazorOptions(options =>
                {
                    options.AddCloudscribeCommonEmbeddedViews();  
                    options.AddCloudscribeNavigationBootstrap3Views();
                    options.AddEmbeddedViewsForSimpleAuth();
                    options.AddCloudscribeSimpleContentBootstrap3Views();
                    options.AddCloudscribeFileManagerBootstrap3Views();

                });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(
            IApplicationBuilder app,
            IHostingEnvironment env,
            ILoggerFactory loggerFactory,
            IOptions<cloudscribe.Web.SimpleAuth.Models.SimpleAuthSettings> authSettingsAccessor
            )
        {
            loggerFactory.AddConsole(Configuration.GetSection("Logging"));
            loggerFactory.AddDebug();

            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseBrowserLink();
            }
            else
            {
                app.UseExceptionHandler("/Home/Error");
            }

            app.UseStaticFiles();

            app.UseCloudscribeCommonStaticFiles();

            // custom 404 and error page - this preserves the status code (ie 404)
            app.UseStatusCodePagesWithReExecute("/Home/Error/{0}");
            
            // this is the cookie auth setup needed for SimpleAuth
            // if you want to integrate with an existing app that already has
            // some authentication implemented then you can remove this and
            // plugin your own custom IProjectSecurityResolver by DI
            var authCookieOptions = new CookieAuthenticationOptions();
            authCookieOptions.AuthenticationScheme = "application";
            authCookieOptions.LoginPath = new PathString("/login");
            authCookieOptions.AccessDeniedPath = new PathString("/");
            authCookieOptions.AutomaticAuthenticate = true;
            authCookieOptions.AutomaticChallenge = true;
            authCookieOptions.CookieName = "application";
            app.UseCookieAuthentication(authCookieOptions);

            

            app.UseMvc(routes =>
            {
                routes.AddSimpleContentStaticResourceRoutes();
                routes.AddCloudscribeFileManagerRoutes();
                routes.AddStandardRoutesForSimpleContent();
                // the Pages feature routes by default would take over as the defualt route
                // if you only want the blog then comment out the line above and use:
                //routes.AddBlogRoutesForSimpleContent();
                // then you will see the standard home controller becomes the home page
                // instead of the pages feature - to use that you would also want to edit
                // the navigation.xml file to remove the pages feature treebuilder reference 
                // and add the other actions of the home controller into the menu
                

                // this route is needed for the SimpleAuth /Login
                routes.MapRoute(
                    name: "def",
                    template: "{controller}/{action}"
                    );

                // this route is not really the default route unless you change the above
                routes.MapRoute(
                    name: "default",
                    template: "{controller=Home}/{action=Index}/{id?}");
            });
        }

        private void ConfigureAuthPolicy(IServiceCollection services)
        {
            //https://docs.asp.net/en/latest/security/authorization/policies.html

            services.AddAuthorization(options =>
            {
                // this policy currently means any user with a blogId claim can edit
                // would require somthing more for multi tenant blogs
                options.AddPolicy(
                    "BlogEditPolicy",
                    authBuilder =>
                    {
                        authBuilder.RequireRole("Administrators");
                    }
                 );

                options.AddPolicy(
                   "PageEditPolicy",
                   authBuilder =>
                   {
                       authBuilder.RequireRole("Administrators");
                   });

                options.AddPolicy(
                    "FileManagerPolicy",
                    authBuilder =>
                    {
                        authBuilder.RequireRole("Administrators");
                    });

                options.AddPolicy(
                    "FileManagerDeletePolicy",
                    authBuilder =>
                    {
                        authBuilder.RequireRole("Administrators");
                    });

                // add other policies here 

            });

        }

    }
}
