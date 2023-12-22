using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace FileCopyApplication
{
    public partial class Form1 : Form
    {

        private bool isPaused = false;
        private CancellationTokenSource cancellationTokenSource;
        private ManualResetEventSlim pauseEvent = new ManualResetEventSlim(true);
        private TaskCompletionSource<bool> taskCompletionSource = new TaskCompletionSource<bool>();
        public Form1()
        {
            InitializeComponent();
        }

        private async void btnStart_Click(object sender, EventArgs e)
        {
            string sourcePath = txtSourcePath.Text;
            string destinationPath = txtDestinationPath.Text;

            if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destinationPath))
            {
                MessageBox.Show("Пожалуйста, выберите пути для копирования.");
                return;
            }

            if (!File.Exists(sourcePath))
            {
                MessageBox.Show("Исходный файл не существует.");
                return;
            }

            int numThreads;
            if (!int.TryParse(txtNumThreads.Text, out numThreads) || numThreads <= 0)
            {
                MessageBox.Show("Введите корректное количество потоков для копирования.");
                return;
            }

            progressBar.Minimum = 0;
            progressBar.Value = 0;

            // Получаем размер файла для вычисления прогресса
            long fileSize = new FileInfo(sourcePath).Length;
          

            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;


            // Запускаем задачи для копирования
            Task[] tasks = new Task[numThreads];
            using (FileStream fsSource = new FileStream(sourcePath, FileMode.Open, FileAccess.Read))
            {
                for (int i = 0; i < numThreads; i++)
                {
                    int index = i;
                    tasks[i] = Task.Run(() =>
                    {
                        long startPosition = index * (fileSize / numThreads);
                        long endPosition = (index == numThreads - 1) ? fileSize : startPosition + (fileSize / numThreads);

                        using (FileStream fsDestination = new FileStream($"{destinationPath}\\Part_{index}.dat", FileMode.Create, FileAccess.Write))
                        {
                            byte[] buffer = new byte[4096];
                            int bytesRead;

                            fsSource.Seek(startPosition, SeekOrigin.Begin);

                            while (startPosition < endPosition && (bytesRead = fsSource.Read(buffer, 0, buffer.Length)) > 0)
                            {
                                fsDestination.Write(buffer, 0, bytesRead);
                                startPosition += bytesRead;

                                // Обновляем прогресс в главном потоке
                                this.Invoke((MethodInvoker)delegate
                                {
                                    int progress = (int)(((double)startPosition / fileSize) * 100);
                                    progressBar.Value = progress;
                                   

                                });
                                while (isPaused) // Приостановка копирования
                                {
                                    Task.Delay(100); // Ждем для обработки возможности продолжения или остановки
                                    if (token.IsCancellationRequested)
                                    {
                                        return; // Остановка копирования, если запросили отмену
                                    }
                                }
                            }
                        }
                    }, token);
                }

                    await Task.WhenAll(tasks);
            }

            if (cancellationTokenSource != null)
            {
                cancellationTokenSource.Dispose();
            }

            MessageBox.Show("Копирование завершено!");
        }
        private void btnBrowseSource_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            if (openFileDialog.ShowDialog() == DialogResult.OK)
            {
                txtSourcePath.Text = openFileDialog.FileName;
            }
        }

        private void btnBrowseDestination_Click(object sender, EventArgs e)
        {
            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
            {
                txtDestinationPath.Text = folderBrowserDialog.SelectedPath;
            }
        }
       //кнопка приостановить
        private void button_Click(object sender, EventArgs e)
        {
            pauseEvent.Reset();
        }

        private void btnResume_Click(object sender, EventArgs e)
        {
            pauseEvent.Set();
        }

        private void buttonStop_Click(object sender, EventArgs e)
        {

            taskCompletionSource.TrySetResult(true);
            txtSourcePath.Clear();
            txtDestinationPath.Clear();
            txtNumThreads.Clear();
            progressBar.Value = 0;
        }
    }
}
