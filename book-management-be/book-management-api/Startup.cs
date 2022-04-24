using System;
using System.Net;
using System.Security.Authentication;
using System.Text;
using System.Threading.Tasks;
using book_management_helpers;
using book_management_helpers.Configurations;
using book_management_persistence.Contexts;
using book_management_persistence.Implements;
using book_management_persistence.Repositories;
using book_management_services.Implements;
using book_management_services.Services;
using MailKit;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

namespace book_management_api
{
    public class Startup
    {
        private readonly IWebHostEnvironment _env;

        public Startup(IWebHostEnvironment env, IConfiguration configuration)
        {
            _env = env;
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            // services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
            services.AddAutoMapper(typeof(AutoMapperProfile));
            services.AddOptions();

            services.AddControllers()
                .AddNewtonsoftJson(opt =>
                {
                    opt.SerializerSettings.ReferenceLoopHandling = ReferenceLoopHandling.Ignore;
                    opt.SerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                    opt.SerializerSettings.Formatting = Formatting.Indented;
                });

            services.AddSwaggerGen(c =>
            {
                c.SwaggerDoc("v1", new OpenApiInfo {Title = "book_management_api", Version = "v1"});
            });

            services.AddDbContext<AppDbContext>(options =>
                options.UseSqlServer(Configuration.GetConnectionString("DefaultConnections"),
                        b => b.MigrationsAssembly("book-management-api"))
                    .LogTo(Console.WriteLine, LogLevel.Information));


            // configure strongly typed settings objects
            var appSettingsSection = Configuration.GetSection("AppSettings");
            services.Configure<AppSettings>(appSettingsSection);

            // configure jwt authentication
            var appSettings = appSettingsSection.Get<AppSettings>();
            var key = Encoding.ASCII.GetBytes(appSettings.Secret);
            services.AddAuthentication(x =>
                {
                    x.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
                    x.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
                })
                .AddJwtBearer(x =>
                {
                    x.Events = new JwtBearerEvents
                    {
                        OnTokenValidated = context =>
                        {
                            var userService = context.HttpContext.RequestServices.GetRequiredService<IUserService>();
                            if (context.Principal?.Identity?.Name != null)
                            {
                                var userId = Guid.Parse(context.Principal?.Identity?.Name);
                                var user = userService.GetById(userId);
                                if (user == null)
                                {
                                    // return unauthorized if user no longer exists
                                    context.Fail("Unauthorized");
                                }
                            }

                            return Task.CompletedTask;
                        }
                    };
                    x.RequireHttpsMetadata = false;
                    x.SaveToken = true;
                    x.TokenValidationParameters = new TokenValidationParameters
                    {
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(key),
                        ValidateIssuer = false,
                        ValidateAudience = false
                    };
                });

            services.AddCors(options =>
            {
                options.AddPolicy("CorsPolicy", builder => builder.WithOrigins("http://localhost:3000")
                    .AllowCredentials()
                    .AllowAnyMethod()
                    .AllowAnyHeader());
            });


            //Cloudinary config
            services.Configure<CloudinarySettings>(Configuration.GetSection("CloudinarySettings"));
            
            //Mailkit config
            services.Configure<EmailConfiguration>(Configuration.GetSection("EmailConfiguration"));
            
            services.AddSingleton<SmtpClient>((serviceProvider) =>
            {
                var config = serviceProvider.GetRequiredService<IConfiguration>();
                var host = config.GetValue<string>("EmailConfiguration:MailServerAddress");
                var port = config.GetValue<string>("EmailConfiguration:MailServerPort");
                var r = new NetworkCredential(config.GetValue<string>("EmailConfiguration:UserId"), config.GetValue<string>("EmailConfiguration:UserPassword"));

                var smtp = new SmtpClient
                {
                    SslProtocols = SslProtocols.None,
                    SslCipherSuitesPolicy = null,
                    ClientCertificates = null,
                    CheckCertificateRevocation = false,
                    ServerCertificateValidationCallback = null,
                    LocalEndPoint = null,
                    ProxyClient = null,
                    Capabilities = SmtpCapabilities.None,
                    Timeout = 0,
                    LocalDomain = null,
                    DeliveryStatusNotificationType = DeliveryStatusNotificationType.Unspecified
                };

                smtp.ServerCertificateValidationCallback = (s, c, h, e) => true;
                smtp.Connect(host, Convert.ToInt32(port));
                smtp.Authenticate(r);
                return smtp;
            });

            // Repositories register
            services.AddScoped<IUnitOfWorks, UnitOfWorks>();
            services.AddScoped<IBookRepository, BookRepositoryImpl>();
            services.AddScoped<ICategoryRepository, CategoryRepositoryImpl>();
            services.AddScoped<IPublisherRepository, PublisherRepositoryImpl>();
            services.AddScoped<IAuthorRepository, AuthorRepositoryImpl>();
            services.AddScoped<IPhotoRepository, PhotoRepositoryImpl>();
            services.AddScoped<ICartRepository, CartRepositoryImpl>();
            services.AddScoped<ICartItemRepository, CartItemRepositoryImpl>();
            services.AddScoped<IOrderRepository, OrderRepositoryImpl>();
            services.AddScoped<IOrderItemRepository, OrderItemRepositoryImpl>();
            services.AddScoped<IUserRepository, UserRepositoryImpl>();

            // Services register
            services.AddScoped<IBookService, BookServiceImpl>();
            services.AddScoped<IUserService, UserServiceImpl>();
            services.AddScoped<ICategoryService, CategoryServiceImpl>();
            services.AddScoped<IPublisherService, PublisherServiceImpl>();
            services.AddScoped<IAuthorService, AuthorServiceImpl>();
            services.AddScoped<IPhotoService, PhotoServiceImpl>();
            services.AddScoped<ICartService, CartServiceImpl>();
            services.AddScoped<ICartItemService, CartItemServiceImpl>();
            services.AddScoped<IOrderService, OrderServiceImpl>();
            services.AddScoped<IEmailService, EmailServiceImpl>();
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            if (env.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseSwagger();
                app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "book_management_api v1"));
            }

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "My API V1");
                c.RoutePrefix = string.Empty;
            });
            app.UseExceptionHandler("/error");
            app.UseRouting();
            app.UseHttpsRedirection();
            app.UseCors("CorsPolicy");
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => { endpoints.MapControllers(); });
        }
    }
}