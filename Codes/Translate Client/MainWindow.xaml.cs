﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Translate.Core.Translator.Baidu;
using Translate.Core.Translator.Bing;
using Translate.Core.Translator.Entities;
using Translate.Core.Translator.Google;
using Translate.Core.Translator.Youdao;
using Translate.Settings;
using Clipboard = System.Windows.Clipboard;
using KeyEventArgs = System.Windows.Input.KeyEventArgs;
using RadioButton = System.Windows.Controls.RadioButton;

namespace Translate_Client
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            _settings = OptionsSettings.Settings;
            InitializeComponent();
        }
        #region fields

        private Settings _settings;

        /// <summary>
        /// The work thread of translate
        /// </summary>
        private Thread _workThread;

        /// <summary>
        /// The thread of to set the status bar text to be ready
        /// </summary>
        private Thread _readyThread;
        /// <summary>
        /// 
        /// </summary>
       private TranslateType _translateType = TranslateType.Google;

        /// <summary>
        /// Next clipboard viewer window 
        /// </summary>
        private IntPtr _hWndNextViewer;
        /// <summary>
        /// The <see cref="HwndSource"/> for this window.
        /// </summary>
        private HwndSource _hWndSource;

        private NotifyIcon _notifyIcon = new NotifyIcon();
        #endregion

        #region The event of window loaded, go to listen Clipboard
        /// <summary>
        /// The event of window loaded, go to listen Clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void win_Loaded(object sender, RoutedEventArgs e)
        {
            SetSettingText(GoogleTranslator.GetSourceLanguages(), GoogleTranslator.GetTargetLanguages(), _settings.GoogleSettings);

            txtSource.Text = Clipboard.GetText();

            InitNotifyIcon();
        }
        #endregion

        private void SetTip(string tipText,string tipTitle)
        {
            _notifyIcon.BalloonTipText = tipText;//这是气球提示的文本
            _notifyIcon.BalloonTipTitle = tipTitle;//这是气球提示的标题
            _notifyIcon.ShowBalloonTip(4000);//气泡显示时间
        }

        private void InitNotifyIcon()
        {
            _notifyIcon.Icon = Properties.Resources.translate;
            _notifyIcon.Visible = true;
            _notifyIcon.Text = @"Translate Client";
            _notifyIcon.MouseClick += ShowMain_Click;

            MenuItem showMainMenu = new MenuItem("显示主窗体");
            showMainMenu.Click += ShowMain_Click;


            MenuItem checkUpdateMenu = new MenuItem("检查更新");
            checkUpdateMenu.Click += CheckUpdateMenu_Click;

            MenuItem checkUpdateWhenStartMenu = new MenuItem("启动时检查更新");
        

            MenuItem updateMenu = new MenuItem("更新", new[] { checkUpdateMenu ,new MenuItem("-"), checkUpdateWhenStartMenu });
           
            MenuItem aboutMenu = new MenuItem("关于");
            aboutMenu.Click += AboutMenu_Click;

            MenuItem exitMenu = new MenuItem("退出");
            exitMenu.Click += ExitMenu_Click;

            //关联托盘控件
            MenuItem[] childen = { showMainMenu, new MenuItem("-"), updateMenu, aboutMenu, new MenuItem("-"), exitMenu };
            _notifyIcon.ContextMenu = new ContextMenu(childen);
        }

        private void CheckUpdateMenu_Click(object sender, EventArgs e)
        {
            SetTip("检查更新", "标题");
        }

        private void AboutMenu_Click(object sender, EventArgs e)
        {
            Process.Start("http://www.zhanghuanglong.com/");
        }

        private void ExitMenu_Click(object sender, EventArgs e)
        {
           Close();
        }

        private void ShowMain_Click(object sender, EventArgs e)
        {
            this.Visibility = Visibility.Visible;
        }

        #region set the textbox's content to be empty
        /// <summary>
        /// set the textbox's content to be empty
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNew_OnClick(object sender, RoutedEventArgs e)
        {
            AbortWrokThread();
            txtSource.Text = "";
            ClearTargetText();
            SetStatusText("New successed...");
        }
        #endregion

        #region copy the target text to clipboard
        /// <summary>
        /// copy the target text to clipboard
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnCopy_OnClick(object sender, RoutedEventArgs e)
        {
            string targetText = txtTarget.Text;
            if (!string.IsNullOrWhiteSpace(targetText))
            {
                try
                {
                    Clipboard.SetText(txtTarget.Text);
                    SetStatusText("Copy successed...");
                }
                catch (Exception exception)
                {
                    SetStatusText("Copy failed...");
                    SetTargetText(exception.Message);
                }
            }
        }
        #endregion

        #region set the source textbox's text to be text from Clipboard
        private void btnPaste_OnClick(object sender, RoutedEventArgs e)
        {
            AbortWrokThread();
            txtSource.Text = Clipboard.GetText();
            PreparationAndTranslation(txtSource.Text);
        }
        #endregion

        #region when click Ctrl + Enter to translate
        /// <summary>
        /// when click Ctrl + Enter to translate
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void txtSource_OnKeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyboardDevice.IsKeyDown(Key.LeftCtrl) || e.KeyboardDevice.IsKeyDown(Key.RightCtrl))
            {
                switch (e.Key)
                {
                    case Key.Enter:
                        var sourceText = txtSource.Text;
                        PreparationAndTranslation(sourceText);
                        break;
                        //case Key.A:
                        //    txtSource.SelectAll();
                        //    break;
                        //case Key.C:
                        //    Clipboard.SetText(txtSource.SelectedText);
                        //    break;
                        //case Key.X:
                        //    if (txtSource.SelectionLength <= 0)
                        //    {
                        //        txtSource.Text = string.Empty;
                        //    }
                        //    else
                        //    {
                        //        Clipboard.SetText(txtSource.SelectedText);
                        //        var text = txtSource.Text;
                        //        txtSource.Text = text.Substring(0, txtSource.SelectionStart) + text.Substring(txtSource.SelectionStart + txtSource.SelectionLength);
                        //    }
                        //    break;
                }
            }
        }
        #endregion

        #region set previous translate source text
        /// <summary>
        /// set previous translate source text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnPrev_OnClick(object sender, RoutedEventArgs e)
        {
            AbortWrokThread();

        }
        #endregion

        #region  set the next translate source text
        /// <summary>
        /// set the next translate source text
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnNext_OnClick(object sender, RoutedEventArgs e)
        {
            AbortWrokThread();

        }
        #endregion

        #region when click settings's button to show options windows
        /// <summary>
        /// when click settings's button to show options windows
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnSettings_OnClick(object sender, RoutedEventArgs e)
        {
            OptionsSettings.ShowOptions();
        }
        #endregion

        #region when click translate button to translate text from source textbox
        /// <summary>
        /// when click translate button to translate text from source textbox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTranslate_OnClick(object sender, RoutedEventArgs e)
        {
            var sourceText = txtSource.Text;
            PreparationAndTranslation(sourceText);
        }
        #endregion

        #region  To set setting button's text when translate type was changed 
        /// <summary>
        /// To set setting button's text when translate type was changed 
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void rbTranslateType_Checked(object sender, RoutedEventArgs e)
        {
            RadioButton radioButton = sender as RadioButton;
            if (string.IsNullOrWhiteSpace(radioButton?.Tag.ToString()))
            {
                return;
            }
            _translateType = (TranslateType)Enum.Parse(typeof(TranslateType), radioButton.Tag.ToString());

            switch (_translateType)
            {
                case TranslateType.Google:
                    SetSettingText(GoogleTranslator.GetSourceLanguages(), GoogleTranslator.GetTargetLanguages(), _settings.GoogleSettings);
                    break;
                case TranslateType.Bing:
                    SetSettingText(BingTranslator.GetSourceLanguages(), BingTranslator.GetTargetLanguages(), _settings.BingSettings);
                    break;
                case TranslateType.Baidu:
                    SetSettingText(BaiduTranslator.GetSourceLanguages(), BaiduTranslator.GetTargetLanguages(), _settings.BaiduSettings);
                    break;
                case TranslateType.Youdao:
                    SetSettingText(YoudaoTranslator.GetSourceLanguages(), YoudaoTranslator.GetTargetLanguages(), _settings.YoudaoSettings);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }
        #endregion


        #region when all translate are completed, will callback this method
        /// <summary>
        /// when all translate are completed, will callback this method
        /// </summary>
        private void TransRequest_OnAllTranslationComplete()
        {
            SetStatusText("Translate successed...");
            SetReady();
        }
        #endregion

        #region  when any translate is completed, will callback this method
        /// <summary>
        /// when any translate is completed, will callback this method
        /// </summary>
        /// <param name="translateResult"></param>
        private void TransRequest_OnTranslationComplete(TranslateResult translateResult)
        {
            SetTargetText(translateResult.TargetText);
        }
        #endregion

        #region methods

        #region Sets the text content on the settings button
        /// <summary>
        /// Sets the text content on the settings button
        /// </summary>
        /// <param name="sourLanguages"></param>
        /// <param name="targetLanguages"></param>
        /// <param name="settings"></param>
        private void SetSettingText(List<TranslationLanguage> sourLanguages, List<TranslationLanguage> targetLanguages, TransSettings settings)
        {
            if (lblSettingText == null)
            {
                // when .ctor call InitializeComponent method, this method will be call at the same time, because call rbTranslateType_Checked method  before call win_Loaded method, this time lblSettingText is null
                return;
            }

            //when source language is empty , source language is auto in bing
            var sourceLanguage = sourLanguages[settings.SourceLanguageIndex].Code.ToUpper();

            var from = string.IsNullOrWhiteSpace(sourceLanguage) ? "AUTO" : sourceLanguage;

            lblSettingText.Text = from.ToUpper() + " -> " +
                                 targetLanguages[settings.TargetLanguageIndex].Code.ToUpper() + " -> " +
                                 targetLanguages[settings.LastLanguageIndex].Code.ToUpper();
        }
        #endregion


        #region Clear the target text and set the enabled status of copy button to be disabled
        /// <summary>
        /// Clear the target text and set the enabled status of copy button to be disabled
        /// </summary>
        private void ClearTargetText()
        {
            txtTarget.Text = "";
            btnCopy.IsEnabled = false;
        }
        #endregion

        #region To abort work thread of the last translate, set status's text to be Treanslating and clear target textbox's text
        /// <summary>
        /// To abort work thread of the last translate, set status's text to be Treanslating and clear target textbox's text
        /// </summary>
        /// <param name="sourceText"></param>
        private void PreparationAndTranslation(string sourceText)
        {
            AbortWrokThread();
            lblStatus.Text = "Translating...";
            ClearTargetText();
            _workThread = new Thread(() =>
            {
                try
                {
                    Translate(sourceText);
                }
                catch (Exception exception)
                {
                    SetTargetText(exception.Message);
                }
            })
            { IsBackground = true };
            _workThread.Start();
        }
        #endregion

        #region To translate
        /// <summary>
        /// To translate
        /// </summary>
        /// <param name="sourceText"></param>
        private void Translate(string sourceText)
        {
            //splite text before translate
            sourceText = OptionsSettings.SpliteLetterByRules(sourceText);
            var translator = TranslatorFactory.GetTranslator(_translateType);
            TranslationRequest transRequest = new TranslationRequest(sourceText, new List<Trans>()
            {
                new Trans()
                {
                    Translator = translator,
                    SourceLanguage = TranslatorFactory.GetSourceLanguage(_translateType, sourceText),
                    TargetLanguage = TranslatorFactory.GetTargetLanguage(_translateType, sourceText),
                }
            });
            transRequest.OnTranslationComplete += TransRequest_OnTranslationComplete;
            transRequest.OnAllTranslationComplete += TransRequest_OnAllTranslationComplete;
        }
        #endregion

        #region  Abort the translate work thread
        /// <summary>
        /// Abort the translate work thread
        /// </summary>
        private void AbortWrokThread()
        {
            _workThread?.Abort();
        }
        #endregion

        #region To set the text of statusbar
        /// <summary>
        /// To set the text of statusbar
        /// </summary>
        /// <param name="text"></param>
        private void SetStatusText(string text)
        {
            Dispatcher.BeginInvoke(new Action(() => { lblStatus.Text = text; }));
        }
        #endregion

        #region To set the text to be ready of the statusbar
        /// <summary>
        /// To set the text to be ready of the statusbar
        /// </summary>
        private void SetReady()
        {
            _readyThread = new Thread(() =>
            {
                Thread.Sleep(3000);
                SetStatusText("ready...");
            })
            { IsBackground = true };
            _readyThread.Start();
        }
        #endregion

        #region Set target text and set the copy button to be enabled and transparency is 1
        /// <summary>
        /// Set target text and set the copy button to be enabled and transparency is 1
        /// </summary>
        /// <param name="targetText"></param>
        private void SetTargetText(string targetText)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                txtTarget.Text = targetText;
                if (!string.IsNullOrWhiteSpace(txtTarget.Text))
                {
                    btnCopy.IsEnabled = true;
                    btnCopy.Opacity = 1;
                }
            }));
        }
        #endregion

        #endregion
    }
}
