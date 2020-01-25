using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using System.IO;
using System.Windows.Threading;
using System.Diagnostics;

using Environment = System.Environment;
using Convert = System.Convert;
using Exception = System.Exception;
using Uri = System.Uri;
using UriKind = System.UriKind;
using Math = System.Math;

namespace SpriteViewer
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //background variables
        private string m_spriteSheetPath = "";
        private string m_backgroundImagePath = "";
        private bool m_isRunning;
        private int m_currentFrame;
        private bool m_newImageLoaded;
        private Stopwatch m_sw = new Stopwatch();

        //user variables
        private int m_framerate;
        private int m_sheetElements;
        private int m_startFrame;
        private int m_animationLength;
        private Vector m_sheetDimensions = new Vector(1,1);
        private Vector m_frameResolution;
        private Vector m_frameBorder;
        private Vector m_atlasResolution;

        public MainWindow()
        {
            m_isRunning = true;
            m_backgroundImagePath = Environment.CurrentDirectory + @"\Data\Grid.bmp";

            InitializeComponent();

            m_sw.Start();

            //async task for update loop
            TaskUpdate();
        }

        async Task TaskUpdate()
        {
            while(m_isRunning)
            {
                await Dispatcher.Yield(DispatcherPriority.Background);

                Update();
            }
        }

        private void Update()
        {
            UpdateValues();

            UpdateViewer();
        }

        private void UpdateValues()
        {
            //update framerate including displaytext
            m_framerate = (int)Slider_fps.Value;
            TextBox_fps.Text = m_framerate.ToString();

            //save textentries
            if (TextBox_SpriteSheetFrames.Text != "")
            {
                m_sheetElements = Convert.ToInt32(TextBox_SpriteSheetFrames.Text);
                TextBox_SpriteSheetFrames.Text = Math.Max(m_sheetElements, 1).ToString();
            }
            if (TextBox_StartFrame.Text != "")
            {
                int tmp = m_startFrame;
                m_startFrame = Convert.ToInt32(TextBox_StartFrame.Text);
                TextBox_StartFrame.Text = Math.Max(Math.Min(m_startFrame, m_sheetElements), 1).ToString();
                if(tmp != m_startFrame)
                {
                    m_currentFrame = m_startFrame - 1;
                }
            }
            if (TextBox_FrameNumber.Text != "")
            {
                m_animationLength = Convert.ToInt32(TextBox_FrameNumber.Text);
                TextBox_FrameNumber.Text = Math.Max(Math.Min(m_animationLength, m_sheetElements - m_startFrame + 1), 1).ToString();
            }
            if (TextBox_BorderThicknessX.Text != "")
            {
                m_frameBorder.X = Convert.ToDouble(TextBox_BorderThicknessX.Text);
            }
            if (TextBox_BorderThicknessY.Text != "")
            {
                m_frameBorder.Y = Convert.ToDouble(TextBox_BorderThicknessY.Text);
            }            

            //readjust texts to prevent errors
            if (Image_View.Source != null
                && TextBox_FrameResolutionX.Text != ""
                && Convert.ToInt32(TextBox_FrameResolutionX.Text) > m_atlasResolution.X)
            {
                TextBox_FrameResolutionX.Text = m_atlasResolution.X.ToString();
            }
            if (Image_View.Source != null
                && TextBox_FrameResolutionY.Text != ""
                && Convert.ToInt32(TextBox_FrameResolutionY.Text) > m_atlasResolution.Y)
            {
                TextBox_FrameResolutionY.Text = m_atlasResolution.Y.ToString();
            }

            //calculate sheetdimensions in single frames
            if (Image_View.Source != null)
            {
                m_sheetDimensions.X = m_atlasResolution.X / (m_frameResolution.X + m_frameBorder.X);
                m_sheetDimensions.Y = m_atlasResolution.Y / (m_frameResolution.Y + m_frameBorder.Y);

                //dont allow more frames than possible with current setup
                if(m_sheetElements > m_sheetDimensions.X * m_sheetDimensions.Y)
                {
                    TextBox_SpriteSheetFrames.Text = (m_sheetDimensions.X * m_sheetDimensions.Y).ToString();
                }
            }
        }

        private void UpdateViewer()
        {
            Int32Rect rect = new Int32Rect();
            
            //next frame
            if (m_framerate > 0
                && m_sw.ElapsedMilliseconds >= 1000 / m_framerate)
            {
                m_currentFrame++;
                if(m_currentFrame >= m_startFrame + m_animationLength - 1)
                {
                    m_currentFrame = m_startFrame - 1;
                }
                m_sw.Restart();
            }

            //calculate position of frame within framegrid
            int column = m_currentFrame % (int)m_sheetDimensions.X;
            int row = m_currentFrame / (int)m_sheetDimensions.X;
            
            //translate gridposition into rect
            rect.X = column * (int)(m_frameResolution.X + m_frameBorder.X);
            rect.Y = row * (int)(m_frameResolution.Y + m_frameBorder.Y);
            rect.Width = (int)(m_frameResolution.X - 2 * m_frameBorder.X);
            rect.Height = (int)(m_frameResolution.Y - 2 * m_frameBorder.Y);

            try
            {
                //draw background first
                if (File.Exists(m_backgroundImagePath))
                {
                    ImageBrush_ViewBackground.ImageSource = new BitmapImage(new Uri(m_backgroundImagePath, UriKind.Absolute));
                }
                //draw current frame
                if (File.Exists(m_spriteSheetPath))
                {
                    BitmapImage bi = new BitmapImage();
                    bi.BeginInit();
                    bi.UriSource = new Uri(m_spriteSheetPath, UriKind.Absolute);
                    bi.CacheOption = BitmapCacheOption.OnLoad;
                    bi.CreateOptions = BitmapCreateOptions.IgnoreImageCache;
                    bi.EndInit();

                    //reset values when new image was loaded
                    if(m_newImageLoaded)
                    {
                        Slider_fps.Value = 0;
                        TextBox_BorderThicknessX.Text = (0).ToString();
                        TextBox_BorderThicknessY.Text = (0).ToString();
                        TextBox_SpriteSheetFrames.Text = (1).ToString();

                        rect.X = 0;
                        rect.Y = 0;
                        rect.Width = (int)bi.Width;
                        rect.Height = (int)bi.Height;

                        m_frameResolution.X = rect.Width;
                        m_frameResolution.Y = rect.Height;
                        m_atlasResolution.X = m_frameResolution.X;
                        m_atlasResolution.Y = m_frameResolution.Y;

                        TextBox_FrameResolutionX.Text = rect.Width.ToString();
                        TextBox_FrameResolutionY.Text = rect.Height.ToString();

                        m_newImageLoaded = false;
                    }

                    CroppedBitmap cbi = new CroppedBitmap(bi, rect);
                    Image_View.Source = cbi;
                }
            }
            catch (Exception _ex)
            {
                WriteStatus("!ERROR! while viewer update: " + _ex);
            }
        }

        private void WriteStatus(string _message)
        {
            TextBlock_Statusbar.Text += (_message + "\n");
        }

        private void ImageDialog()
        {
            OpenFileDialog fileDialog = new OpenFileDialog()
            {
                Filter = "Image Files(*.png)|*.png|Image Files(*.jpg)|*.jpg|Image Files(*.bmp)|*.bmp",
                Multiselect = false
            };
            bool? isImageSelected = fileDialog.ShowDialog();
            
            if(isImageSelected == true)
            {
                string filePath = fileDialog.FileName;

                if (File.Exists(filePath))
                {
                    WriteStatus("Loading " + filePath + "...");
                    LoadImage(filePath);
                }
            }
            else if(isImageSelected == false)
            {
                //request wether anything was selected to prevent errormessage when closing dialog
                if (fileDialog.FileName != "")
                {
                    WriteStatus("!ERROR! while selecting image: Selection has an unallowed filetype (.png, .jpg and .bmp are allowed filetypes)");
                }
            }
            else
            {
                WriteStatus("!ERROR! while slecting image: Selection is null");
            }       
        }

        private void LoadImage(string _path)
        {
            string newPath = Environment.CurrentDirectory + @"\Data\" + _path.Split('\\').Last();

            try
            {
                //delete last loaded image
                if (m_spriteSheetPath != "")
                {
                    DeleteImage(m_spriteSheetPath);
                    m_spriteSheetPath = "";
                }
                //cload only when its a new image
                if (newPath != m_spriteSheetPath)
                {
                    //...and its not in correct directory yet
                    if (!File.Exists(newPath))
                    {
                        File.Copy(_path, newPath);
                    }
                    m_spriteSheetPath = newPath;
                    m_newImageLoaded = true;

                    WriteStatus("Image loaded: " + m_spriteSheetPath);
                }
            }
            catch (Exception _ex)
            {
                WriteStatus("!ERROR! while loading image: " + _ex);
            }
        }

        private void DeleteImage(string _path)
        {
            try
            {
                if (File.Exists(_path))
                {
                    Image_View.Source = null;
                    File.Delete(_path);
                }
            }
            catch(Exception _ex)
            {
                WriteStatus("!ERROR! while deleting image: " + _ex);
            }
        }

        private void ImportImage(object sender, RoutedEventArgs e)
        {
            ImageDialog();
        }
        
        private void OnWindowClosing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            string[] filePaths = Directory.GetFiles(Environment.CurrentDirectory + @"\Data\");

            //delete all foreign files
            for (int i = 0; i < filePaths.Length; i++)
            {
                if(filePaths[i] == m_backgroundImagePath)
                {
                    continue;
                }

                DeleteImage(filePaths[i]);
            }
        }

        private void LimitToNumbers(object sender, TextCompositionEventArgs e)
        {
            //only allows numeric input
            if (!char.IsDigit(e.Text, e.Text.Length - 1))
            {
                e.Handled = true;
            }
        }

        private void OnStartFrameChange(object sender, TextChangedEventArgs e)
        {
            //reset spritesheet animation
            if (TextBox_StartFrame.Text != "")
            {
                m_currentFrame = m_startFrame - 1;
            }
        }

        private void OnEnterDown(object sender, KeyEventArgs e)
        {
            //update frame resolutions by pressing enter only
            if (e.IsDown
                && e.Key == Key.Enter)
            {
                if (sender == TextBox_FrameResolutionX)
                {
                    m_frameResolution.X = Convert.ToDouble(TextBox_FrameResolutionX.Text);
                    TextBox_FrameResolutionX.Text = Math.Max(m_frameResolution.X, 100).ToString();
                }
                else if (sender == TextBox_FrameResolutionY)
                {
                    m_frameResolution.Y = Convert.ToDouble(TextBox_FrameResolutionY.Text);
                    TextBox_FrameResolutionY.Text = Math.Max(m_frameResolution.Y, 100).ToString();
                }
            }
        }

        private void OnMenuExitClick(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void OnMenuHelpClick(object sender, RoutedEventArgs e)
        {
            AboutWindow aboutWindow = new AboutWindow();
            aboutWindow.ShowDialog();
        }
    }
}
