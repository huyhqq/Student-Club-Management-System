using System;
using System.Text;

namespace ClubManagementApi.Services
{
    public static class EmailTemplate
    {
        // Màu sắc chính thức FPT CLUB (2025)
        private const string Primary = "#E94E1B";   // Cam FPT
        private const string PrimaryDark = "#D83A0A";
        private const string Accent = "#0A4D68";   // Xanh đậm
        private const string LightBg = "#F8F9FA";
        private const string WarningBg = "#FFF4E5";
        private const string DangerBg = "#FFEBEE";

        public static string GenerateActivationEmailTemplate(string userName, string activationLink, DateTime expiryDate)
        {
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html>");
            sb.Append("<html lang='vi'>");
            sb.Append("<head>");
            sb.Append("    <meta charset='UTF-8'>");
            sb.Append("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("    <title>Kích hoạt tài khoản • FPT CLUB</title>");
            sb.Append("    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap' rel='stylesheet'>");
            sb.Append("    <style>");
            sb.Append("        * { margin:0; padding:0; box-sizing:border-box; }");
            sb.Append("        body { font-family:'Inter',-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif; background:#f5f7fa; color:#333; line-height:1.6; padding:20px 0; }");
            sb.Append("        .container { max-width:600px; margin:0 auto; background:#fff; border-radius:16px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.08); }");
            sb.Append("        .header { background:linear-gradient(135deg," + Primary + "," + PrimaryDark + "); padding:40px 30px; text-align:center; color:#fff; }");
            sb.Append("        .header img { height:70px; margin-bottom:16px; }");
            sb.Append("        .header h1 { font-size:28px; font-weight:700; margin-bottom:8px; }");
            sb.Append("        .header p { font-size:16px; opacity:0.95; }");
            sb.Append("        .content { padding:44px 40px; text-align:center; }");
            sb.Append("        .greeting { font-size:20px; font-weight:600; color:" + Accent + "; margin-bottom:16px; }");
            sb.Append("        .message { font-size:16px; color:#444; margin-bottom:32px; line-height:1.7; }");
            sb.Append("        .btn { display:inline-block; background:" + Primary + "; color:#fff; font-weight:600; font-size:16px; padding:14px 32px; border-radius:12px; text-decoration:none; box-shadow:0 4px 15px rgba(233,78,27,0.3); transition:all .3s; }");
            sb.Append("        .btn:hover { background:" + PrimaryDark + "; transform:translateY(-2px); box-shadow:0 8px 20px rgba(233,78,27,0.4); }");
            sb.Append("        .info-box { margin:32px 0; padding:20px; background:" + WarningBg + "; border-left:5px solid " + Primary + "; border-radius:0 12px 12px 0; text-align:left; }");
            sb.Append("        .info-box h3 { color:" + PrimaryDark + "; font-size:15px; margin-bottom:8px; font-weight:600; }");
            sb.Append("        .info-box p { font-size:14px; color:#555; }");
            sb.Append("        .warning-box { background:" + DangerBg + "; border-left-color:#e91e63; }");
            sb.Append("        .footer { background:" + LightBg + "; padding:32px; text-align:center; font-size:13px; color:#666; border-top:1px solid #eee; }");
            sb.Append("        .footer-brand { font-size:20px; font-weight:700; color:" + Primary + "; margin-bottom:8px; }");
            sb.Append("        .footer a { color:" + Primary + "; text-decoration:none; font-weight:500; }");
            sb.Append("        @media(max-width:600px){ .content{padding:32px 24px;} .header{padding:32px 20px;} .header h1{font-size:24px;} .btn{padding:12px 28px; font-size:15px;} }");
            sb.Append("    </style>");
            sb.Append("</head>");
            sb.Append("<body>");
            sb.Append("    <div class='container'>");
            sb.Append("        <div class='header'>");
            sb.Append("            <img src='https://nld.mediacdn.vn/291774122806476800/2025/10/25/fpt-gia-tri-thuong-hieu-1-ti-do-176135613386939028057.png' alt='FPT CLUB Logo'>");
            sb.Append("            <h1>Chào mừng đến với FPT CLUB</h1>");
            sb.Append("            <p>Chỉ còn một bước để hoàn tất đăng ký</p>");
            sb.Append("        </div>");
            sb.Append("        <div class='content'>");
            sb.Append("            <div class='greeting'>Xin chào " + userName + "!</div>");
            sb.Append("            <div class='message'>");
            sb.Append("                Cảm ơn bạn đã đăng ký thành viên FPT CLUB.<br>");
            sb.Append("                Vui lòng nhấn vào nút bên dưới để kích hoạt tài khoản và bắt đầu tham gia các hoạt động, sự kiện độc quyền.");
            sb.Append("            </div>");
            sb.Append("            <a href='" + activationLink + "' class='btn'>Kích hoạt tài khoản ngay</a>");

            sb.Append("            <div class='info-box'>");
            sb.Append("                <h3>Thời hạn kích hoạt</h3>");
            sb.Append("                <p>Liên kết này sẽ <strong>hết hạn vào " + expiryDate.ToString("dd/MM/yyyy HH:mm:ss") + "</strong>.<br>");
            sb.Append("                Vui lòng kích hoạt trước thời gian trên để tránh phải đăng ký lại.</p>");
            sb.Append("            </div>");

            sb.Append("            <div class='info-box warning-box'>");
            sb.Append("                <h3>Bảo mật tài khoản</h3>");
            sb.Append("                <p>Nếu bạn <strong>không thực hiện đăng ký</strong> này, vui lòng bỏ qua email và liên hệ ngay Ban Quản lý qua <a href='mailto:club@fpt.edu.vn'>club@fpt.edu.vn</a>.</p>");
            sb.Append("            </div>");
            sb.Append("        </div>");

            sb.Append("        <div class='footer'>");
            sb.Append("            <div class='footer-brand'>FPT CLUB</div>");
            sb.Append("            <p>FPT University • TP. Hồ Chí Minh</p>");
            sb.Append("            <p>Email: <a href='mailto:club@fpt.edu.vn'>club@fpt.edu.vn</a> • Hotline: 0865 341 645</p>");
            sb.Append("            <p>© 2025 FPT CLUB • Tất cả quyền được bảo lưu.</p>");
            sb.Append("        </div>");
            sb.Append("    </div>");
            sb.Append("</body>");
            sb.Append("</html>");

            return sb.ToString();
        }

        public static string GenerateOtpEmailTemplate(string userName, string otpCode, DateTime expiryDate)
        {
            var sb = new StringBuilder();

            sb.Append("<!DOCTYPE html>");
            sb.Append("<html lang='vi'>");
            sb.Append("<head>");
            sb.Append("    <meta charset='UTF-8'>");
            sb.Append("    <meta name='viewport' content='width=device-width, initial-scale=1.0'>");
            sb.Append("    <title>Mã OTP Đặt lại mật khẩu • FPT CLUB</title>");
            sb.Append("    <link href='https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&display=swap' rel='stylesheet'>");
            sb.Append("    <style>");
            sb.Append("        * { margin:0; padding:0; box-sizing:border-box; }");
            sb.Append("        body { font-family:'Inter',-apple-system,BlinkMacSystemFont,'Segoe UI',Roboto,sans-serif; background:#f5f7fa; color:#333; line-height:1.6; padding:20px 0; }");
            sb.Append("        .container { max-width:600px; margin:0 auto; background:#fff; border-radius:16px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.08); }");
            sb.Append("        .header { background:linear-gradient(135deg," + Primary + "," + PrimaryDark + "); padding:40px 30px; text-align:center; color:#fff; }");
            sb.Append("        .header img { height:70px; margin-bottom:16px; }");
            sb.Append("        .header h1 { font-size:28px; font-weight:700; margin-bottom:8px; }");
            sb.Append("        .header p { font-size:16px; opacity:0.95; }");
            sb.Append("        .content { padding:44px 40px; text-align:center; }");
            sb.Append("        .greeting { font-size:20px; font-weight:600; color:" + Accent + "; margin-bottom:16px; }");
            sb.Append("        .message { font-size:16px; color:#444; margin-bottom:32px; line-height:1.7; }");
            sb.Append("        .otp-box { background:#fff8e8; border:2px dashed " + Primary + "; border-radius:16px; padding:32px 20px; margin:32px 0; }");
            sb.Append("        .otp-code { font-size:36px; font-weight:700; letter-spacing:8px; color:" + PrimaryDark + "; font-family:'Courier New',monospace; background:#fff; padding:16px; border-radius:12px; }");
            sb.Append("        .info-box { margin:32px 0; padding:20px; background:" + WarningBg + "; border-left:5px solid " + Primary + "; border-radius:0 12px 12px 0; text-align:left; }");
            sb.Append("        .info-box h3 { color:" + PrimaryDark + "; font-size:15px; margin-bottom:8px; font-weight:600; }");
            sb.Append("        .info-box p { font-size:14px; color:#555; }");
            sb.Append("        .warning-box { background:" + DangerBg + "; border-left-color:#e91e63; }");
            sb.Append("        .footer { background:" + LightBg + "; padding:32px; text-align:center; font-size:13px; color:#666; border-top:1px solid #eee; }");
            sb.Append("        .footer-brand { font-size:20px; font-weight:700; color:" + Primary + "; margin-bottom:8px; }");
            sb.Append("        .footer a { color:" + Primary + "; text-decoration:none; font-weight:500; }");
            sb.Append("        @media(max-width:600px){ .content{padding:32px 24px;} .otp-code{font-size:28px; letter-spacing:5px;} }");
            sb.Append("    </style>");
            sb.Append("</head>");
            sb.Append("<body>");
            sb.Append("    <div class='container'>");
            sb.Append("        <div class='header'>");
            sb.Append("            <img src='https://nld.mediacdn.vn/291774122806476800/2025/10/25/fpt-gia-tri-thuong-hieu-1-ti-do-176135613386939028057.png' alt='FPT CLUB Logo'>");
            sb.Append("            <h1>Đặt lại mật khẩu</h1>");
            sb.Append("            <p>Mã xác minh một lần (OTP)</p>");
            sb.Append("        </div>");
            sb.Append("        <div class='content'>");
            sb.Append("            <div class='greeting'>Xin chào " + userName + "!</div>");
            sb.Append("            <div class='message'>");
            sb.Append("                Chúng tôi đã nhận được yêu cầu đặt lại mật khẩu cho tài khoản FPT CLUB của bạn.<br>");
            sb.Append("                Vui lòng sử dụng mã OTP dưới đây để tiếp tục:");
            sb.Append("            </div>");

            sb.Append("            <div class='otp-box'>");
            sb.Append("                <div class='otp-code'>" + otpCode + "</div>");
            sb.Append("            </div>");

            sb.Append("            <div class='info-box'>");
            sb.Append("                <h3>Thời hạn sử dụng</h3>");
            sb.Append("                <p>Mã OTP chỉ có hiệu lực đến <strong>" + expiryDate.ToString("dd/MM/yyyy HH:mm:ss") + "</strong>.<br>");
            sb.Append("                Sau thời gian này, bạn cần yêu cầu mã mới.</p>");
            sb.Append("            </div>");

            sb.Append("            <div class='info-box warning-box'>");
            sb.Append("                <h3>Cảnh báo bảo mật</h3>");
            sb.Append("                <p>FPT CLUB <strong>không bao giờ</strong> yêu cầu bạn cung cấp mã OTP qua điện thoại hoặc tin nhắn.<br>");
            sb.Append("                Nếu bạn không yêu cầu, vui lòng liên hệ ngay: <a href='mailto:club@fpt.edu.vn'>club@fpt.edu.vn</a></p>");
            sb.Append("            </div>");
            sb.Append("        </div>");

            sb.Append("        <div class='footer'>");
            sb.Append("            <div class='footer-brand'>FPT CLUB</div>");
            sb.Append("            <p>FPT University • TP. Hồ Chí Minh</p>");
            sb.Append("            <p>Email: <a href='mailto:club@fpt.edu.vn'>club@fpt.edu.vn</a> • Hotline: 0865 341 645</p>");
            sb.Append("            <p>© 2025 FPT CLUB • Tất cả quyền được bảo lưu.</p>");
            sb.Append("        </div>");
            sb.Append("    </div>");
            sb.Append("</body>");
            sb.Append("</html>");

            return sb.ToString();
        }
    }
}