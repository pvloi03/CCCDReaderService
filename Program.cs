using CCCDReaderService.Tray;
using CCCDReaderService.Services;

namespace CCCDReaderService;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        try
        {
            // 1. Khởi tạo cấu hình Windows Forms
            ApplicationConfiguration.Initialize();

            // 2. Khởi tạo máy chủ ASP.NET Core Kestrel
            var builder = WebApplication.CreateBuilder(args);

            var configuredUrl = builder.Configuration["Urls"] ?? "http://localhost:5200";
            builder.WebHost.UseUrls(configuredUrl);

            builder.Services.AddControllers();

            // Đăng ký các dịch vụ cốt lõi (Session & Hardware Lock)
            var sessionTimeout = builder.Configuration.GetValue<int>("ReaderSettings:SessionTimeoutSeconds", 120);
            builder.Services.AddSingleton<ISessionManager>(new Services.SessionManager(sessionTimeout));
            builder.Services.AddSingleton<Services.IHardwareLock, Services.HardwareLock>();

            builder.Services.AddCors(options =>
            {
                options.AddDefaultPolicy(policy =>
                {
                    policy.AllowAnyOrigin()
                          .AllowAnyHeader()
                          .AllowAnyMethod();
                });
            });

            var app = builder.Build();

            app.UseCors();
            app.UseDefaultFiles();
            app.UseStaticFiles();
            app.MapControllers();

            // 3. Khởi động Web Host đồng bộ
            app.StartAsync().GetAwaiter().GetResult();

            // 4. Chạy vòng lặp thông điệp Windows Forms cho Khay hệ thống
            Application.Run(new TrayApplicationContext(app, configuredUrl));

            // 5. Dọn dẹp máy chủ khi thoát
            try
            {
                app.StopAsync(TimeSpan.FromSeconds(3)).GetAwaiter().GetResult();
                app.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch
            {
                // Bỏ qua lỗi khi giải phóng lúc tắt ứng dụng
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Lỗi khởi chạy CCCDReaderService:\n{ex.Message}",
                "Lỗi Hệ Thống",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }
}