using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Reactive;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Threading.Tasks;
using GitHubJwt;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Miro.Models.Checks;
using Miro.Models.Merge;
using Miro.Models.MiroConfig;
using Miro.Services.Auth;
using Miro.Services.Checks;
using Miro.Services.Github;
using Miro.Services.Github.EventHandlers;
using Miro.Services.Merge;
using Miro.Services.MiroConfig;
using Miro.Services.MiroStats;
using MiroConfig;
using MongoDB.Driver;
using Newtonsoft.Json;
using Serilog;
using Serilog.Formatting.Json;
using Swashbuckle.AspNetCore.Swagger;

namespace Miro
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {

            Log.Logger = new LoggerConfiguration()
                .ReadFrom.Configuration(Configuration)
                .Enrich.FromLogContext()
                .WriteTo.Console(new JsonFormatter())
                .CreateLogger();

            services.AddMvc()
                    .SetCompatibilityVersion(CompatibilityVersion.Version_2_1)
                    .AddGitHubWebHooks();

            services.AddMiro();
            services.AddMongoDb(Configuration);
          
            services.AddTransient<MergeRequestsRepository, MergeRequestsRepository>();
            services.AddTransient<ChecksRepository, ChecksRepository>();
            services.AddTransient<RepoConfigRepository, RepoConfigRepository>();
            services.AddTransient<PrMerger, PrMerger>();
            services.AddTransient<PrUpdater, PrUpdater>();
            services.AddTransient<FileRetriever, FileRetriever>();
            services.AddTransient<PrDeleter, PrDeleter>();
            services.AddTransient<PrStatusChecks, PrStatusChecks>();
            services.AddSingleton(new InstallationTokenStore(Configuration));

            services.AddSwaggerGen(c =>
            {
                c.AddSecurityDefinition("Api-Key", new ApiKeyScheme { In = "header", Description = "Please enter the valid API Key", Name = "Authorization", Type = "apiKey" });
                c.AddSecurityRequirement(new Dictionary<string, IEnumerable<string>> {
                { "Api-Key", Enumerable.Empty<string>() },
            });
                c.SwaggerDoc("v1", new Info { Title = "Miro  API", Version = "v1" });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
            }
            else
            {
                app.UseHsts();
                app.ApplyApiKeyValidation();
            }
            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Miro API V1");
            });
            
            app.UseMvc();
        }
    }

    public static class IServiceCollectionExt
    {
        public static IServiceCollection AddMiro(this IServiceCollection services)
        {
            services.AddScoped<MergeabilityValidator, MergeabilityValidator>();
            services.AddScoped<MiroStatsProvider, MiroStatsProvider>();
            services.AddScoped<GithubHttpClient, GithubHttpClient>();
            services.AddScoped<CommentCreator, CommentCreator>();
            services.AddScoped<ReviewsRetriever, ReviewsRetriever>();
            services.AddScoped<IssueCommentEventHandler, IssueCommentEventHandler>();
            services.AddScoped<PullRequestEventHandler, PullRequestEventHandler>();
            services.AddScoped<PullRequestReviewEventHandler, PullRequestReviewEventHandler>();
            services.AddScoped<StatusEventHandler, StatusEventHandler>();
            services.AddScoped<PushEventHandler, PushEventHandler>();
            services.AddScoped<ChecksManager, ChecksManager>();
            services.AddScoped<ChecksRetriever, ChecksRetriever>();
            services.AddScoped<MergeOperations, MergeOperations>();
            services.AddScoped<MiroMergeCheck, MiroMergeCheck>();
            services.AddScoped<RepoConfigManager, RepoConfigManager>();

            return services;
        }

        public static IServiceCollection AddMongoDb(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddSingleton(new MongoClient(configuration["MONGO_CONNECTION_STRING"]));
            services.AddScoped(p => p.GetService<MongoClient>()
                                               .GetDatabase("miro-db")
                                               .GetCollection<MergeRequest>("merge-requests"));
            services.AddScoped(p => p.GetService<MongoClient>()
                                     .GetDatabase("miro-db")
                                     .GetCollection<CheckList>("check-lists"));
            services.AddScoped(p => p.GetService<MongoClient>()
                                     .GetDatabase("miro-db")
                                     .GetCollection<RepoConfig>("repo-config"));

            return services;
        }
    }
}
