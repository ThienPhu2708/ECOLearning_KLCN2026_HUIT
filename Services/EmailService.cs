using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ECOLearning.Services
{
    public static class EmailService
    {
        private static (bool Success, string Message) SendEmailInternal(string recipientEmail, string recipientName, string subject, string htmlBody)
        {
            try
            {
                string smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                int smtpPort = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int port) ? port : 587;
                string smtpEmail = ConfigurationManager.AppSettings["SmtpEmail"] ?? "";
                string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
                bool enableSsl = bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out bool ssl) ? ssl : true;
                string senderName = ConfigurationManager.AppSettings["SmtpSenderName"] ?? "Hệ thống E-Learning";

                if (string.IsNullOrWhiteSpace(smtpEmail) || string.IsNullOrWhiteSpace(smtpPassword))
                {
                    return (false, "Chưa cấu hình tài khoản SMTP (SmtpEmail/SmtpPassword) trong Web.config.");
                }

                using (MailMessage mail = new MailMessage())
                {
                    mail.From = new MailAddress(smtpEmail, senderName, Encoding.UTF8);
                    mail.To.Add(new MailAddress(recipientEmail, string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName, Encoding.UTF8));
                    mail.Subject = subject;
                    mail.BodyEncoding = Encoding.UTF8;
                    mail.IsBodyHtml = true;
                    mail.Body = htmlBody;

                    using (SmtpClient smtp = new SmtpClient(smtpHost, smtpPort))
                    {
                        smtp.Credentials = new NetworkCredential(smtpEmail, smtpPassword);
                        smtp.EnableSsl = enableSsl;
                        smtp.Send(mail);
                    }
                }

                return (true, "Email đã được gửi thành công!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendEmailInternal Error: " + ex.Message);
                return (false, "Lỗi kết nối máy chủ Mail: " + ex.Message);
            }
        }

        // 1. Gửi OTP Đăng ký tài khoản
        public static (bool Success, string Message) SendOtpEmail(string recipientEmail, string recipientName, string otpCode, string subject = "Mã xác thực OTP đăng ký - E-Learning")
        {
            string displayName = string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName;

            string body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 20px; }}
                    .container {{ max-width: 540px; margin: 0 auto; background: #FFFFFF; border-radius: 16px; overflow: hidden; border: 1px solid #E2E8F0; box-shadow: 0 10px 15px -3px rgba(0,0,0,0.05); }}
                    .header {{ background: #434CE8; color: #FFFFFF; padding: 24px; text-align: center; }}
                    .header h1 {{ margin: 0; font-size: 22px; font-weight: 800; letter-spacing: 0.5px; }}
                    .content {{ padding: 32px 28px; color: #0F172A; line-height: 1.6; }}
                    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
                    .otp-box {{ background: #EEF2FF; border: 2px dashed #C7D2FE; border-radius: 12px; padding: 20px; text-align: center; margin: 24px 0; }}
                    .otp-code {{ font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #434CE8; font-family: monospace; }}
                    .expiry-note {{ font-size: 13px; color: #64748B; text-align: center; margin-top: 8px; }}
                    .warning {{ background: #FFFBEB; border-left: 4px solid #F59E0B; padding: 12px 16px; font-size: 13px; color: #92400E; margin-top: 20px; border-radius: 4px; }}
                    .footer {{ background: #F1F5F9; padding: 16px 28px; text-align: center; font-size: 12px; color: #64748B; border-top: 1px solid #E2E8F0; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>E-Learning<span style='color:#A5B4FC;'>.</span></h1>
                    </div>
                    <div class='content'>
                        <div class='greeting'>Xin chào {displayName},</div>
                        <p>Cảm ơn bạn đã đăng ký tài khoản trên nền tảng E-Learning. Vui lòng sử dụng mã OTP bên dưới để hoàn tất xác thực email:</p>
                        
                        <div class='otp-box'>
                            <div style='font-size: 12px; font-weight: 700; color: #434CE8; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 1px;'>Mã xác thực OTP đăng ký</div>
                            <div class='otp-code'>{otpCode}</div>
                            <div class='expiry-note'>Mã có hiệu lực trong vòng <strong>5 phút</strong>.</div>
                        </div>

                        <div class='warning'>
                            <strong>Lưu ý an toàn:</strong> Tuyệt đối không chia sẻ mã này cho bất kỳ ai để đảm bảo an toàn cho tài khoản của bạn.
                        </div>
                    </div>
                    <div class='footer'>
                        Email này được gửi tự động từ hệ thống E-Learning. Vui lòng không trả lời trực tiếp email này.
                    </div>
                </div>
            </body>
            </html>";

            return SendEmailInternal(recipientEmail, displayName, subject, body);
        }

        // 2. Gửi OTP Khôi phục mật khẩu (Quên mật khẩu)
        public static (bool Success, string Message) SendForgotOtpEmail(string recipientEmail, string recipientName, string otpCode)
        {
            string displayName = string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName;

            string body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 20px; }}
                    .container {{ max-width: 540px; margin: 0 auto; background: #FFFFFF; border-radius: 16px; overflow: hidden; border: 1px solid #E2E8F0; box-shadow: 0 10px 15px -3px rgba(0,0,0,0.05); }}
                    .header {{ background: #434CE8; color: #FFFFFF; padding: 24px; text-align: center; }}
                    .header h1 {{ margin: 0; font-size: 22px; font-weight: 800; letter-spacing: 0.5px; }}
                    .content {{ padding: 32px 28px; color: #0F172A; line-height: 1.6; }}
                    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
                    .otp-box {{ background: #EEF2FF; border: 2px dashed #C7D2FE; border-radius: 12px; padding: 20px; text-align: center; margin: 24px 0; }}
                    .otp-code {{ font-size: 34px; font-weight: 800; letter-spacing: 8px; color: #434CE8; font-family: monospace; }}
                    .expiry-note {{ font-size: 13px; color: #64748B; text-align: center; margin-top: 8px; }}
                    .warning {{ background: #FFFBEB; border-left: 4px solid #F59E0B; padding: 12px 16px; font-size: 13px; color: #92400E; margin-top: 20px; border-radius: 4px; }}
                    .footer {{ background: #F1F5F9; padding: 16px 28px; text-align: center; font-size: 12px; color: #64748B; border-top: 1px solid #E2E8F0; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>E-Learning<span style='color:#A5B4FC;'>.</span></h1>
                    </div>
                    <div class='content'>
                        <div class='greeting'>Xin chào {displayName},</div>
                        <p>Chúng tôi nhận được yêu cầu khôi phục mật khẩu cho tài khoản liên kết với địa chỉ email <strong>{recipientEmail}</strong>.</p>
                        <p>Vui lòng nhập mã OTP xác thực 6 số dưới đây để tiếp tục tạo mật khẩu mới:</p>
                        
                        <div class='otp-box'>
                            <div style='font-size: 12px; font-weight: 700; color: #434CE8; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 1px;'>Mã xác thực khôi phục mật khẩu</div>
                            <div class='otp-code'>{otpCode}</div>
                            <div class='expiry-note'>Mã xác thực có hiệu lực trong vòng <strong>5 phút</strong>.</div>
                        </div>

                        <div class='warning'>
                            <strong>Lưu ý bảo mật:</strong> Nếu bạn không yêu cầu đặt lại mật khẩu, hãy bỏ qua email này hoặc đăng nhập để kiểm tra bảo mật tài khoản. Tuyệt đối không cung cấp mã OTP cho người khác.
                        </div>
                    </div>
                    <div class='footer'>
                        Email này được gửi tự động từ hệ thống E-Learning. Vui lòng không trả lời trực tiếp email này.
                    </div>
                </div>
            </body>
            </html>";

            return SendEmailInternal(recipientEmail, displayName, "[E-Learning] Mã OTP xác nhận khôi phục mật khẩu", body);
        }

        // 3. Gửi email xác nhận đặt lại mật khẩu thành công
        public static (bool Success, string Message) SendPasswordResetConfirmationEmail(string recipientEmail, string recipientName)
        {
            string displayName = string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName;
            string timeStr = DateTime.Now.ToString("HH:mm, dd/MM/yyyy");

            string body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 20px; }}
                    .container {{ max-width: 540px; margin: 0 auto; background: #FFFFFF; border-radius: 16px; overflow: hidden; border: 1px solid #E2E8F0; box-shadow: 0 10px 15px -3px rgba(0,0,0,0.05); }}
                    .header {{ background: #10B981; color: #FFFFFF; padding: 24px; text-align: center; }}
                    .header h1 {{ margin: 0; font-size: 22px; font-weight: 800; letter-spacing: 0.5px; }}
                    .content {{ padding: 32px 28px; color: #0F172A; line-height: 1.6; }}
                    .greeting {{ font-size: 16px; font-weight: 600; margin-bottom: 12px; }}
                    .success-card {{ background: #ECFDF5; border: 1px solid #A7F3D0; border-radius: 12px; padding: 18px 20px; margin: 20px 0; }}
                    .success-title {{ color: #065F46; font-weight: 700; font-size: 15px; }}
                    .success-desc {{ font-size: 13px; color: #047857; margin-top: 6px; }}
                    .warning {{ background: #FFFBEB; border-left: 4px solid #F59E0B; padding: 12px 16px; font-size: 13px; color: #92400E; margin-top: 20px; border-radius: 4px; }}
                    .footer {{ background: #F1F5F9; padding: 16px 28px; text-align: center; font-size: 12px; color: #64748B; border-top: 1px solid #E2E8F0; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'>
                        <h1>E-Learning<span style='color:#D1FAE5;'>.</span></h1>
                    </div>
                    <div class='content'>
                        <div class='greeting'>Xin chào {displayName},</div>
                        <p>Thông báo bảo mật: Mật khẩu cho tài khoản E-Learning của bạn (<strong>{recipientEmail}</strong>) đã được cập nhật thành công vào lúc <strong>{timeStr}</strong>.</p>
                        
                        <div class='success-card'>
                            <div class='success-title'>✓ Đặt lại mật khẩu thành công</div>
                            <div class='success-desc'>Tài khoản của bạn đã được cập nhật mật khẩu mới. Bạn có thể sử dụng mật khẩu này để đăng nhập ngay bây giờ.</div>
                        </div>

                        <div class='warning'>
                            <strong>Cảnh báo bảo mật:</strong> Nếu bạn KHÔNG thực hiện thay đổi này, tài khoản của bạn có thể đã bị xâm nhập. Vui lòng liên hệ với ban quản trị E-Learning ngay lập tức để được hỗ trợ khóa và bảo vệ tài khoản.
                        </div>
                    </div>
                    <div class='footer'>
                        Email này được gửi tự động từ hệ thống E-Learning. Vui lòng không trả lời trực tiếp email này.
                    </div>
                </div>
            </body>
            </html>";

            return SendEmailInternal(recipientEmail, displayName, "[E-Learning] Thông báo bảo mật: Mật khẩu tài khoản đã được thay đổi thành công", body);
        }

        // 4. Gửi tin nhắn từ Form Liên hệ (Trang Contact) đến hộp thư hỗ trợ của hệ thống
        public static (bool Success, string Message) SendContactMessage(string senderName, string senderEmail, string subject, string content)
        {
            string supportEmail = ConfigurationManager.AppSettings["SmtpEmail"] ?? "";
            if (string.IsNullOrWhiteSpace(supportEmail))
            {
                return (false, "Chưa cấu hình email hỗ trợ (SmtpEmail) trong Web.config.");
            }

            string safeName = System.Net.WebUtility.HtmlEncode(senderName);
            string safeEmail = System.Net.WebUtility.HtmlEncode(senderEmail);
            string safeSubject = System.Net.WebUtility.HtmlEncode(subject);
            string safeContent = System.Net.WebUtility.HtmlEncode(content).Replace("\n", "<br/>");
            string timeStr = DateTime.Now.ToString("HH:mm, dd/MM/yyyy");

            string body = $@"
            <!DOCTYPE html>
            <html>
            <head>
                <meta charset='utf-8' />
                <style>
                    body {{ font-family: 'Segoe UI', Arial, sans-serif; background-color: #F8FAFC; margin: 0; padding: 20px; }}
                    .container {{ max-width: 560px; margin: 0 auto; background: #FFFFFF; border-radius: 16px; overflow: hidden; border: 1px solid #E2E8F0; }}
                    .header {{ background: #434CE8; color: #FFFFFF; padding: 24px; text-align: center; }}
                    .header h1 {{ margin: 0; font-size: 20px; font-weight: 800; }}
                    .content {{ padding: 28px; color: #0F172A; line-height: 1.6; }}
                    .field {{ margin-bottom: 14px; }}
                    .field-label {{ font-size: 12px; font-weight: 700; color: #64748B; text-transform: uppercase; letter-spacing: 0.5px; }}
                    .field-value {{ font-size: 14px; color: #0F172A; margin-top: 2px; }}
                    .message-box {{ background: #F8FAFC; border: 1px solid #E2E8F0; border-radius: 10px; padding: 16px; margin-top: 16px; font-size: 14px; }}
                </style>
            </head>
            <body>
                <div class='container'>
                    <div class='header'><h1>Liên hệ mới từ Website E-Learning</h1></div>
                    <div class='content'>
                        <div class='field'><div class='field-label'>Họ tên</div><div class='field-value'>{safeName}</div></div>
                        <div class='field'><div class='field-label'>Email liên hệ</div><div class='field-value'>{safeEmail}</div></div>
                        <div class='field'><div class='field-label'>Tiêu đề</div><div class='field-value'>{safeSubject}</div></div>
                        <div class='field'><div class='field-label'>Thời gian gửi</div><div class='field-value'>{timeStr}</div></div>
                        <div class='message-box'>{safeContent}</div>
                    </div>
                </div>
            </body>
            </html>";

            return SendEmailInternal(supportEmail, "Ban Quản Trị E-Learning", "[Liên hệ Website] " + subject, body);
        }
    }
}
