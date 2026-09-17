using System;
using System.Configuration;
using System.Net;
using System.Net.Mail;
using System.Text;

namespace ECOLearning.Services
{
    public static class EmailService
    {
        public static (bool Success, string Message) SendOtpEmail(string recipientEmail, string recipientName, string otpCode, string subject = "Mã xác thực OTP - E-Learning")
        {
            try
            {
                string smtpHost = ConfigurationManager.AppSettings["SmtpHost"] ?? "smtp.gmail.com";
                int smtpPort = int.TryParse(ConfigurationManager.AppSettings["SmtpPort"], out int port) ? port : 587;
                string smtpEmail = ConfigurationManager.AppSettings["SmtpEmail"] ?? "";
                string smtpPassword = ConfigurationManager.AppSettings["SmtpPassword"] ?? "";
                bool enableSsl = bool.TryParse(ConfigurationManager.AppSettings["SmtpEnableSsl"], out bool ssl) ? ssl : true;
                string senderName = ConfigurationManager.AppSettings["SmtpSenderName"] ?? "E-Learning Platform";

                // Nếu chưa cấu hình email hoặc mật khẩu ứng dụng SMTP
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

                    string displayName = string.IsNullOrWhiteSpace(recipientName) ? recipientEmail : recipientName;

                    mail.Body = $@"
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
                            .otp-code {{ font-size: 32px; font-weight: 800; letter-spacing: 8px; color: #434CE8; font-family: monospace; }}
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
                                <p>Bạn vừa yêu cầu mã xác thực OTP để thực hiện thao tác trên hệ thống E-Learning.</p>
                                
                                <div class='otp-box'>
                                    <div style='font-size: 13px; font-weight: 600; color: #434CE8; margin-bottom: 6px; text-transform: uppercase; letter-spacing: 1px;'>Mã xác thực OTP của bạn</div>
                                    <div class='otp-code'>{otpCode}</div>
                                    <div class='expiry-note'>⏰ Mã này có hiệu lực trong vòng <strong>5 phút</strong>.</div>
                                </div>

                                <div class='warning'>
                                    ⚠️ <strong>Lưu ý an toàn:</strong> Không chia sẻ mã này với bất kỳ ai, kể cả nhân viên hỗ trợ của E-Learning.
                                </div>
                            </div>
                            <div class='footer'>
                                Email này được gửi tự động từ hệ thống E-Learning. Vui lòng không trả lời trực tiếp email này.
                            </div>
                        </div>
                    </body>
                    </html>";

                    using (SmtpClient smtp = new SmtpClient(smtpHost, smtpPort))
                    {
                        smtp.Credentials = new NetworkCredential(smtpEmail, smtpPassword);
                        smtp.EnableSsl = enableSsl;
                        smtp.Send(mail);
                    }
                }

                return (true, "Mã xác thực OTP đã được gửi đến email thành công!");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine("SendOtpEmail Error: " + ex.Message);
                return (false, "Lỗi kết nối máy chủ Mail: " + ex.Message);
            }
        }
    }
}
