using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.HttpsPolicy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Pisstaube.Core;
using StackExchange.Redis;

namespace Pisstaube.API
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
            services.AddControllers();
            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "Pisstaube.API", Version = "v1"});
            });

            string redisConnectionString;
            {
                var host = Environment.GetEnvironmentVariable("REDIS_HOST");
                var port = Environment.GetEnvironmentVariable("REDIS_PORT");
                var pass = Environment.GetEnvironmentVariable("REDIS_PASSWORD");
                var database = Environment.GetEnvironmentVariable("REDIS_DATABASE");
                
                var connString = $"{host}";
                if (!string.IsNullOrEmpty(port))
                    connString += $":{port}";
                if (!string.IsNullOrEmpty(pass))
                    connString += $",password={pass}";
                if (!string.IsNullOrEmpty(database))
                    connString += $",defaultDatabase={database}";
                
                redisConnectionString = connString;
            }

            var conn = ConnectionMultiplexer.Connect(redisConnectionString);
            
            services.AddSingleton(conn);
            services.AddSingleton<IConnectionMultiplexer>(conn);
            services.AddSingleton<RedisEventTool>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "Pisstaube.API v1"));
            }

            app.UseHttpsRedirection();

            app.UseRouting();

            app.UseAuthorization();

            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}