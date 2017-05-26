﻿namespace BSolutions.SerialKeyGeneratorConverter
{
    using Microsoft.Win32;
    using System;
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Data.OleDb;
    using System.Data.SQLite;
    using System.Windows;

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public Dictionary<int, string> SerialHashs { get; set; } = new Dictionary<int, string>();

        private readonly BackgroundWorker worker = new BackgroundWorker();

        public MainWindow()
        {
            InitializeComponent();

            this.worker.WorkerReportsProgress = true;
            this.worker.WorkerSupportsCancellation = true;
            this.worker.DoWork += new DoWorkEventHandler(worker_DoWork);
            this.worker.ProgressChanged += new ProgressChangedEventHandler(worker_ProgressChanged);
            this.worker.RunWorkerCompleted += new RunWorkerCompletedEventHandler(worker_RunWorkerCompleted);
        }

        #region --- Background Worker ---

        private void worker_DoWork(object sender, DoWorkEventArgs e)
        {
            string database = (string)e.Argument;
            int itemsFinished = 0;

            SQLiteConnection.CreateFile(database);

            using (var connection = new SQLiteConnection($"Data Source={database}"))
            {
                connection.Open();

                // Create Hash Table
                var command = new SQLiteCommand("CREATE TABLE Hash (ID INTEGER PRIMARY KEY AUTOINCREMENT NOT NULL, SerialHash TEXT NOT NULL)", connection);
                command.ExecuteNonQuery();

                foreach (var row in this.SerialHashs)
                {
                    if (!this.worker.CancellationPending)
                    {
                        command = new SQLiteCommand($"INSERT INTO Hash (SerialHash) VALUES ('{row.Value}')", connection);
                        command.ExecuteNonQuery();

                        this.worker.ReportProgress(itemsFinished++);
                    }
                }

                command.Dispose();
            }

            GC.Collect();
            e.Result = CrypthographyHelper.SHA256File(database);
        }

        private void worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            this.PbProcess.Value = e.ProgressPercentage;
        }

        private void worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            MessageBox.Show($"New license file successfully created!\n\nChecksum: {e.Result.ToString()}");

            this.TxtChecksum.Text = e.Result.ToString();
            this.NavOpen.IsEnabled = true;
            this.NavStartConvert.IsEnabled = true;
        }

        #endregion

        private void NavOpen_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                CheckFileExists = true,
                Filter = "License Files (*.lic)|*.lic|Database Files (*.db)|*.db|All files (*.*)|*.*",
                Title = "Please select a license file to convert"
            };

            if (openFileDialog.ShowDialog() == true)
            {
                try
                {
                    this.ReadLicenseFile(openFileDialog.FileName);

                    this.LblSourceLicenseFile.Content = openFileDialog.FileName;
                    this.LblQuantitySerialHashs.Content = this.SerialHashs.Count;
                    this.PbProcess.Maximum = this.SerialHashs.Count - 1;

                    this.GridSerialHashs.ItemsSource = this.SerialHashs;
                    this.GridContent.Visibility = Visibility.Visible;
                    this.NavConvert.IsEnabled = true;
                    this.PbProcess.Value = 0;
                }
                catch (Exception)
                {
                    MessageBox.Show("The source is no valid license file.");
                }
            }
        }

        private void NavStartConvert_Click(object sender, RoutedEventArgs e)
        {
            this.NavStartConvert.IsEnabled = false;
            this.NavOpen.IsEnabled = false;

            SaveFileDialog saveFileDialog = new SaveFileDialog()
            {
                Filter = "License Files (*.lic)|*.lic|Database Files (*.db)|*.db|All files (*.*)|*.*",
                Title = "Please create a new license file for convertion"
            };

            if (saveFileDialog.ShowDialog() == true)
            {
                this.worker.RunWorkerAsync(saveFileDialog.FileName);
            }
        }

        private void NavExit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ReadLicenseFile(string database)
        {
            this.SerialHashs.Clear();
            string connectionString = $"Provider=Microsoft.Jet.OLEDB.4.0;Data Source={database};Persist Security Info=False;";

            using (var connection = new OleDbConnection(connectionString))
            {
                connection.Open();

                var command = new OleDbCommand("SELECT * FROM Hash", connection);
                var reader = command.ExecuteReader();

                while (reader.Read())
                {
                    this.SerialHashs.Add(reader.GetInt32(0), reader.GetString(1));
                }
            }
        }
    }
}
