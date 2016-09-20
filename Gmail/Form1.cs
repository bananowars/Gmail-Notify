using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Resources;
using System.Threading;

namespace Gmail
{
    public partial class Form1 : Form
    {
        NotifyIcon notifyIcon1;
        ContextMenu contextMenu1;
        MenuItem exit;
        MenuItem exitAccount;
        MenuItem autoRun;

        System.Windows.Forms.Timer timer;
        System.Net.Sockets.TcpClient tcpc = null;
        System.Net.Security.SslStream ssl = null;

        string username, password;
        string tempStr;
        int UnReadMessageCount;
        int bytes = -1;
        byte[] buffer;

        StringBuilder answer;
        StringBuilder sb = new StringBuilder();

        byte[] request;
        bool delAcc = false;

        public void Designer()
        {
            components = new Container();
            contextMenu1 = new ContextMenu();

            timer = new System.Windows.Forms.Timer();
            timer.Interval = 333;
            timer.Tick += new EventHandler(timer_Tick);

            autoRun = new MenuItem();
            autoRun.Text = "Автозапуск";
            autoRun.Click += new System.EventHandler(autoRun_Click);
            autoRun.Checked = false;
            contextMenu1.MenuItems.AddRange(new MenuItem[] { autoRun });

            exitAccount = new MenuItem();
            exitAccount.Text = "Выход из аккаунта";
            exitAccount.Click += new System.EventHandler(exitAccount_Click);
            exitAccount.Enabled = false;
            contextMenu1.MenuItems.AddRange(new MenuItem[] { exitAccount });

            exit = new MenuItem();
            exit.Text = "Выход";
            exit.Click += new System.EventHandler(exit_Click);
            contextMenu1.MenuItems.AddRange(new MenuItem[] { exit });

            notifyIcon1 = new NotifyIcon(components);
            notifyIcon1.ContextMenu = this.contextMenu1;
            notifyIcon1.Icon = Properties.Resources.google;
            notifyIcon1.Text = "Оповещение новых писем Gmail";
            notifyIcon1.DoubleClick += new EventHandler(gotoGmail);
            notifyIcon1.Visible = true;
        }
        public Form1()
        {
            InitializeComponent();
            Designer();
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", true);
            string[] mass = key.GetValueNames();
            for (int i = 0; i < mass.Length; i++)
            {
                if (mass[i] == "Gmail") { autoRun.Checked = true; }
            }
            if (Properties.Settings.Default.ID != Environment.MachineName)
            {
                RemoveAccount();
            }

            if (Properties.Settings.Default.Username != "" && Properties.Settings.Default.Password != "")
            {
                try
                {
                    txbEmail.Text = Properties.Settings.Default.Username;
                    txbPassword.Text = Properties.Settings.Default.Password;
                    username = Properties.Settings.Default.Username;
                    password = Properties.Settings.Default.Password;
                    Connection = true;
                }
                catch
                {
                    ControlsEnable(true);
                    return;
                }
                Account(username, password);
            }
            else
            {
                ControlsEnable(true);
                return;
            }

        }
        private void AddAccount()
        {
            Properties.Settings.Default.Username = username;
            Properties.Settings.Default.Password = password;
            Properties.Settings.Default.ID = Environment.MachineName;
            Properties.Settings.Default.Save();
        }
        private void RemoveAccount()
        {
            Properties.Settings.Default.Username = "";
            Properties.Settings.Default.Password = "";
            Properties.Settings.Default.ID = "";
            Properties.Settings.Default.Save();
        }
        private void autoRun_Click(object sender, EventArgs e)
        {
            if (autoRun.Checked == false) { autoRun.Checked = true; }
            else { autoRun.Checked = false; }
            if (autoRun.Checked == true) { AddautoRun(); }
            else { DeleteautoRun(); }
        }
        void exit_Click(object sender, EventArgs e)
        {
            // Connection = false;
            Close();
        }
        private void ControlsEnable(bool e)
        {
            txbEmail.Enabled = e;
            txbPassword.Enabled = e;
            btnEnter.Enabled = e;
        }
        private void ControlsClear()
        {
            txbEmail.Text = "";
            txbPassword.Text = "";
            txbEmail.Focus();
        }
        void exitAccount_Click(object sender, EventArgs e)
        {
            delAcc = true;
        }
        private void gotoGmail(object sender, EventArgs e)
        {
            System.Diagnostics.Process.Start("https://mail.google.com/mail/u/0/#inbox");
        }
        private async void timer_Tick(object sender, EventArgs e)
        {
            timer.Enabled = false;
            if (!tcpc.Connected)
            {
                try
                {
                    Connection = true;
                }
                catch
                {
                    notifyIcon1.BalloonTipClicked -= new EventHandler(gotoGmail);
                    notifyIcon1.BalloonTipIcon = ToolTipIcon.Warning;
                    notifyIcon1.BalloonTipTitle = "Соединение";
                    notifyIcon1.BalloonTipText = "Проблемы с подключением";
                    notifyIcon1.ShowBalloonTip(10);
                    Show();
                    ControlsEnable(true);
                    return;
                }
            }
                await Task.Run(() =>
             {
                 if (Visible) { Invoke(new Action(() => { Hide(); })); }
                 if (UnReadMessage() < UnReadMessageCount) { UnReadMessageCount = UnReadMessage(); }
                 if (UnReadMessage() > UnReadMessageCount)
                 {
                     notifyIcon1.BalloonTipClicked += new EventHandler(gotoGmail);
                     notifyIcon1.BalloonTipIcon = ToolTipIcon.Info;
                     notifyIcon1.BalloonTipTitle = "Почтовый ящик";
                     notifyIcon1.BalloonTipText = "Непрочитанных сообщений: " + UnReadMessage() + "\nНажмите чтобы посмотреть";
                     notifyIcon1.ShowBalloonTip(30);
                     UnReadMessageCount = UnReadMessage();
                 }
             });
            if (delAcc == true)
            {
                Connection = false;
                RemoveAccount();
                Show();
                ControlsEnable(true);
                exitAccount.Enabled = false;
                UnReadMessageCount = 0;
                ControlsClear();
                delAcc = false;
            }
            else
            {
                timer.Enabled = true;
            }

        }
        private void AddautoRun()
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", true);
            key.SetValue("Gmail", Application.ExecutablePath);
        }
        private void DeleteautoRun()
        {
            var key = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Run\", true);
            key.DeleteValue("Gmail");
        }
        private StringBuilder Request(string command)
        {
            try
            {
                if (command != "")
                {
                    request = Encoding.ASCII.GetBytes(command);
                    ssl.Write(request, 0, request.Length);
                }
                ssl.Flush();
                sb = new StringBuilder();
                buffer = new byte[2048];
                bytes = ssl.Read(buffer, 0, 2048);
                sb.Append(Encoding.ASCII.GetString(buffer));
                return sb;
            }
            catch
            {
                return null;
            }

        }
        private void Account(string user, string pass)
        {
            ControlsEnable(false);
            try
            {
                Connection = true;
            }
            catch
            {
                notifyIcon1.BalloonTipClicked -= new EventHandler(gotoGmail);
                notifyIcon1.BalloonTipIcon = ToolTipIcon.Warning;
                notifyIcon1.BalloonTipTitle = "Соединение";
                notifyIcon1.BalloonTipText = "Проблемы с подключением";
                notifyIcon1.ShowBalloonTip(10);
                ControlsEnable(true);
                return;
            }
            Request("");
            answer = Request("$ LOGIN " + user + " " + pass + "  \r\n");
            if (!Regex.IsMatch(answer.ToString(), @"Success", RegexOptions.Multiline))
            {
                notifyIcon1.BalloonTipClicked -= new EventHandler(gotoGmail);
                notifyIcon1.BalloonTipIcon = ToolTipIcon.Error;
                notifyIcon1.BalloonTipTitle = "Авторизация";
                notifyIcon1.BalloonTipText = "Неверный логин или пароль";
                notifyIcon1.ShowBalloonTip(20);
                ControlsClear();
                ControlsEnable(true);
                delAcc = true;
            }
            else
            {
                AddAccount();
                Hide();
                timer.Enabled = true;
                exitAccount.Enabled = true;
            }
        }
        private bool Connection
        {
            set
            {
                if (value == true)
                {
                    if (ssl != null)
                    {
                        ssl.Close();
                        ssl.Dispose();
                    }
                    if (tcpc != null)
                    {
                        tcpc.Close();
                    }
                    tcpc = new System.Net.Sockets.TcpClient("imap.gmail.com", 993);
                    ssl = new System.Net.Security.SslStream(tcpc.GetStream());
                    ssl.AuthenticateAsClient("imap.gmail.com");
                }
                else
                {
                    if (ssl != null)
                    {
                        ssl.Close();
                        ssl.Dispose();
                    }
                    if (tcpc != null)
                    {
                        tcpc.Close();
                    }
                }
            }

        }
        private int UnReadMessage()
        {
            try
            {
                answer = Request("$ STATUS INBOX (UNSEEN)\r\n");
                tempStr = Regex.Match(answer.ToString(), @"UNSEEN \d+", RegexOptions.Multiline).ToString();
                return Convert.ToInt32(tempStr.Split(' ')[1]);
            }
            catch { return 0; }
        }
        private void enterLogin(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                Login(null, null);
            }
        }
        private void Login(object sender, EventArgs e)
        {
            if (txbEmail.Text == "" || !Regex.IsMatch(txbEmail.Text, @"^\S+"))
            {
                txbEmail.Focus();
                return;
            }
            if (txbPassword.Text == "")
            {
                txbPassword.Focus();
                return;
            }
            username = txbEmail.Text;
            password = txbPassword.Text;
            Account(username, password);
        }
    }
}
