using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using Newtonsoft.Json;
using Microsoft.Extensions.Logging;
using System.Media;
using StudyHelper.Models.Flashcards;
using StudyHelper.Services.Logging;

namespace StudyHelper
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        // Task management
        // _tasks collection that notifies when refreshed or something is added or remove
        private ObservableCollection<Models.Tasks.Task> _tasks = new ObservableCollection<Models.Tasks.Task>();

        // Logging
        // Initialize instance of ILogger into _logger variable
        private readonly ILogger _logger;
        // Initialize instance of ILoggerFactory into _loggerFactory variable
        private readonly ILoggerFactory _loggerFactory;
        
        // Pomodoro Timer Variables
        // Initiazlize instance of DispatcherTimer into _timer variable
        private DispatcherTimer _timer;
        // Time span struct variable
        private TimeSpan _remainingTime;
        private bool _isWorkPeriod = true;
        private bool _isTimerRunning = false;

        // Flashcard variables
        // _flashcardDecks collection that notifies when refreshed or something is added or removed
        private ObservableCollection<FlashcardDeck> _flashcardDecks = new ObservableCollection<FlashcardDeck>();
        // Initialize _currentDeck variable which is an instance of class FlashcardDeck
        private FlashcardDeck _currentDeck;
        private int _currentCardIndex = 0;
        private bool _isShowingFront = true;

        

        /// <summary>
        /// Initializes a new instance of the MainWindow class.
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            // Set up logger factory
            _loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.AddFile($"{DateTime.Now:yyyy-MM-dd}.log");
            });

            // Create configured logger and store in _logger variable
            _logger = _loggerFactory.CreateLogger<MainWindow>();

            // Initialize task list
            lstTasks.ItemsSource = _tasks;


            // Initialize Flashcards
            InitializeFlashcards();

            // Initialize Pomodoro Timer
            InitializeTimer();

            _logger.LogInformation("Application started");
            // txtStatus is a reference to the text block status
            txtStatus.Text = "Application started";
        }

        #region Task Management

        /// <summary>
        ///     Click event 'AddTask' to add a new task in the _tasks Observable Collection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddTask_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtTask.Text))
            {
                LogAndUpdateStatus("Error: Task title cannot be empty", LogLevel.Error);
                return;
            }

            // Get the priority value from the combo box
            string selectedPriority = (cmbPriority.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "Medium";

            // Allow user to choose the task type (work/personal)
            bool isWorkTask = MessageBox.Show("Is this a Work Task?", "Select Task Type",
            MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

            // Create new task from the TaskFactory based on if isWorkTask is true or not
            Models.Tasks.Task newTask = Models.Tasks.TaskFactory.CreateTask(isWorkTask);

            // Set title and priority of the task
            newTask.Title = txtTask.Text;
            newTask.Priority = selectedPriority;

            _tasks.Add(newTask);
            LogAndUpdateStatus($"Added task: {newTask.Title} ({newTask.TaskType}, {newTask.Priority})", LogLevel.Information);

        }

        /// <summary>
        ///     Click event 'SortByName' which sorts the tasks inside the _tasks collection alphabetically
        /// </summary>
        private void SortByName_Click(object sender, RoutedEventArgs e)
        {
            // order the tasks by their Title
            var sortedTasks = _tasks.OrderBy(t => t.Title).ToList();

            _tasks.Clear();
            // add the sorted tasks back to the _tasks variable
            foreach (var task in sortedTasks)
                _tasks.Add(task);

            lstTasks.ItemsSource = _tasks; // re add the tasks to the ItemsSource for the list
            LogAndUpdateStatus("Tasks sorted by name", LogLevel.Information);
        }




        /// <summary>
        ///     Click event 'SortByPriority' which sorts the tasks inside by priority (High being first, Medium second, etc.)
        /// </summary>
        private void SortByPriority_Click(object sender, RoutedEventArgs e)
        {
            // variable to help define the priority to a number
            var priorityOrder = new Dictionary<string, int>
            {
                { "High", 3 },
                { "Medium", 2 },
                { "Low", 1 }
            };

            // variable containing the sorted tasks in descending priority High -> Medium -> Low
            var sortedTasks = _tasks
                .OrderByDescending(t => priorityOrder.ContainsKey(t.Priority) ? priorityOrder[t.Priority] : 0)
                .ToList();

            _tasks.Clear();
            // for each sorted task in the sortedTasks variable add that task back to the _tasks collection
            foreach (var task in sortedTasks)
                _tasks.Add(task);

            lstTasks.ItemsSource = _tasks; // re add the tasks to the ItemsSource for the list
            LogAndUpdateStatus("Tasks sorted by priority", LogLevel.Information);
        }




        /// <summary>
        ///     Click event 'Search' which takes in a string from the textbox and searches within the _tasks collection for the word and displays the task containing that word
        /// </summary>
        private void Search_Click(object sender, RoutedEventArgs e)
        {
            string searchTerm = txtSearch.Text.ToLower();

            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                lstTasks.ItemsSource = _tasks; // Refresh list
                LogAndUpdateStatus("Search cleared, showing all tasks", LogLevel.Information);
                return;
            }

            // variable holding the tasks that have a title matching the searchTerm
            var results = from task in _tasks
                          where task.Title.ToLower().Contains(searchTerm)
                          select task;
            // add the results of the sorting to the listbox
            lstTasks.ItemsSource = results.ToList();
            LogAndUpdateStatus($"Found {results.Count()} tasks matching '{searchTerm}'", LogLevel.Information);
        }



        /// <summary>
        ///     Saves the current tasks to a JSON file in the user's Documents folder.
        ///     serializes the abstract Task class and types that belong to it.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void SaveTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "tasks.json");

                // Configure serializer to include type information of the tasks
                var settings = new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All, 
                    Formatting = Formatting.Indented,
                    NullValueHandling = NullValueHandling.Ignore,
                    ReferenceLoopHandling = ReferenceLoopHandling.Ignore
                };

                // Convert ObservableCollection to List
                List<Models.Tasks.Task> taskList = _tasks.ToList();

                // Make sure tasks are valid before serializing
                foreach (var task in taskList)
                {
                    if (task == null || string.IsNullOrEmpty(task.Title))
                    {
                        _logger.LogWarning("Found a null or invalid task during saving");
                    }
                }

                // Creates a json string
                string json = JsonConvert.SerializeObject(taskList, settings);

                // Log conversion to json string
                _logger.LogInformation($"Serializing {taskList.Count} tasks");
                _logger.LogInformation($"JSON contains type info: {json.Contains("$type")}");

                // creates the file at the path if it doesnt exist and writes the json to it
                File.WriteAllText(path, json);

                // Log some details about the tasks were saving
                _logger.LogInformation($"Tasks saved to: {path}");
                LogAndUpdateStatus($"Tasks saved to: {path}", LogLevel.Information);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error saving tasks: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                LogAndUpdateStatus($"Error saving tasks: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        ///     Loads tasks from a JSON file in the user's Documents folder.
        ///     Handles deserialization of Task class and other types that belong to it.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        private void LoadTasks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "tasks.json");

                // if the file exists in the path we defined
                if (File.Exists(path))
                {
                    // Configure serializer with proper settings
                    var settings = new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All,
                        NullValueHandling = NullValueHandling.Ignore,
                        MissingMemberHandling = MissingMemberHandling.Ignore,
                        Formatting = Formatting.Indented
                    };

                    // variable containing the text read from the tasks.json file
                    string json = File.ReadAllText(path);

                    // Add logging to see the actual JSON content
                    _logger.LogInformation($"Loading JSON: {json}");

                    // For debugging, see if the JSON contains $type information
                    if (!json.Contains("$type"))
                    {
                        _logger.LogWarning("JSON does not contain type information required for deserialization");
                    }

                    // Deserialize to List<Task> from json string
                    var loadedTasks = JsonConvert.DeserializeObject<List<Models.Tasks.Task>>(json, settings);

                    if (loadedTasks != null && loadedTasks.Count > 0)
                    {
                        // Refresh the _tasks collection and if they are not empty add a task
                        _tasks.Clear();
                        foreach (var task in loadedTasks)
                        {
                            // Add extra validation
                            if (task != null && !string.IsNullOrEmpty(task.Title))
                            {
                                _tasks.Add(task);
                            }
                        }

                        // add the tasks collection back to the task list box
                        lstTasks.ItemsSource = _tasks;
                        LogAndUpdateStatus($"Successfully loaded {_tasks.Count} tasks from file", LogLevel.Information);
                    }

                    else
                    {
                        LogAndUpdateStatus("No tasks found in the file or deserialization resulted in null", LogLevel.Warning);
                    }
                }
                else
                {
                    LogAndUpdateStatus("No saved tasks file found", LogLevel.Warning);
                }
            }
            catch (JsonSerializationException ex)
            {
                // logging
                _logger.LogError($"JSON Serialization error: {ex.Message}");
                _logger.LogError($"Error details: {ex.StackTrace}");
                LogAndUpdateStatus($"Serialization error: {ex.Message}", LogLevel.Error);
            }
            catch (JsonReaderException ex)
            {
                // Handle corrupted JSON specifically
                _logger.LogError($"JSON parsing error: {ex.Message}");
                LogAndUpdateStatus($"JSON parsing error: {ex.Message}", LogLevel.Error);
            }
            catch (Exception ex)
            {
                // Catch any other general exceptions
                _logger.LogError($"Error loading tasks: {ex.Message}");
                _logger.LogError($"Stack trace: {ex.StackTrace}");
                LogAndUpdateStatus($"Error loading tasks: {ex.Message}", LogLevel.Error);
            }
        }

        #endregion

        #region Pomodoro Timer

        /// <summary>
        ///     Initializes the timer with the slider value and updates the display
        /// </summary>
        private void InitializeTimer()
        {
            _timer = new DispatcherTimer();
            _timer.Tick += Timer_Tick;
            _timer.Interval = TimeSpan.FromSeconds(1);

            // Initialize with default value from slider
            _remainingTime = TimeSpan.FromMinutes((int)sliderWorkDuration.Value);
            UpdateTimerDisplay();
        }

        /// <summary>
        ///     Method that handles the timer actually counting down and when it is complete
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Timer_Tick(object sender, EventArgs e)
        {
            // if the remaining time is greater than 0 decrement the time and update the timer's display
            if (_remainingTime.TotalSeconds > 0)
            {
                _remainingTime = _remainingTime.Subtract(TimeSpan.FromSeconds(1));
                UpdateTimerDisplay();
            }
            else
            {
                // Timer completed
                _timer.Stop();
                _isTimerRunning = false;

                // Play sound notification
                SystemSounds.Asterisk.Play();

                // Log completion of timer
                string periodType = _isWorkPeriod ? "Work" : "Break";
                string logMessage = $"Completed {periodType} period";
                lstTimerLog.Items.Add($"{DateTime.Now:T} - {logMessage}");
                LogAndUpdateStatus(logMessage, LogLevel.Information);

                // Switch between work and break ( work period = is not work period )
                _isWorkPeriod = !_isWorkPeriod;

                // Set the time for the next period
                if (_isWorkPeriod)
                {
                    _remainingTime = TimeSpan.FromMinutes((int)sliderWorkDuration.Value);
                }
                else
                {
                    _remainingTime = TimeSpan.FromMinutes((int)sliderBreakDuration.Value);
                }

                UpdateTimerDisplay();

                // Update UI
                btnStartTimer.Content = "Start";
                btnStartTimer.IsEnabled = true;
                btnPauseTimer.IsEnabled = false;

                // Auto-start break if option is selected
                if (!_isWorkPeriod && chkAutoStartBreak.IsChecked == true)
                {
                    StartTimer_Click(this, null);
                }
            }
        }

        /// <summary>
        ///     method responsible for updating the timers text and color depending on work/break period
        /// </summary>
        private void UpdateTimerDisplay()
        {
            // timer display 00:00
            txtTimerDisplay.Text = string.Format("{0:D2}:{1:D2}",
                _remainingTime.Minutes,
                _remainingTime.Seconds);

            // Update background color based on work/break period
            if (_isWorkPeriod)
            {
                txtTimerDisplay.Foreground = Brushes.DarkGreen;
            }
            else
            {
                txtTimerDisplay.Foreground = Brushes.Blue;
            }
        }

        /// <summary>
        ///     Click event 'StartTimer' that  starts the DispatchTimer and logs it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void StartTimer_Click(object sender, RoutedEventArgs e)
        {
            // if timer is not running
            if (!_isTimerRunning)
            {
                _timer.Start();
                _isTimerRunning = true;

                // enable/disable buttons
                btnStartTimer.IsEnabled = false;
                btnPauseTimer.IsEnabled = true;

                // log timer start
                string periodType = _isWorkPeriod ? "Work" : "Break";
                string logMessage = $"Started {periodType} timer";
                lstTimerLog.Items.Add($"{DateTime.Now:T} - {logMessage}");
                LogAndUpdateStatus(logMessage, LogLevel.Information);
            }
        }

        /// <summary>
        ///     Click event 'PauseTimer' that stops the DispatchTimer and logs it
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void PauseTimer_Click(object sender, RoutedEventArgs e)
        {
            // if timer is running
            if (_isTimerRunning)
            {
                _timer.Stop();
                _isTimerRunning = false;

                btnStartTimer.Content = "Resume";
                btnStartTimer.IsEnabled = true;
                btnPauseTimer.IsEnabled = false;

                // log timer pause
                string logMessage = "Timer paused";
                lstTimerLog.Items.Add($"{DateTime.Now:T} - {logMessage}");
                LogAndUpdateStatus(logMessage, LogLevel.Information);
            }
        }

        /// <summary>
        ///     Click event 'ResetTimer' that stops the Dispatch timer and resets
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ResetTimer_Click(object sender, RoutedEventArgs e)
        {
            _timer.Stop();
            _isTimerRunning = false;

            // Reset to work period
            _isWorkPeriod = true;
            _remainingTime = TimeSpan.FromMinutes((int)sliderWorkDuration.Value);

            UpdateTimerDisplay();

            btnStartTimer.Content = "Start";
            btnStartTimer.IsEnabled = true;
            btnPauseTimer.IsEnabled = false;

            // log timer reset
            string logMessage = "Timer reset";
            lstTimerLog.Items.Add($"{DateTime.Now:T} - {logMessage}");
            LogAndUpdateStatus(logMessage, LogLevel.Information);
        }
        /// <summary>
        ///     OnChange event that watches the value of the sliders for work and break duration
        ///     and updates the remaining time value
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void TimerSettings_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // if timer is not running
            if (!_isTimerRunning)
            {
                if (_isWorkPeriod)
                {
                    _remainingTime = TimeSpan.FromMinutes((int)sliderWorkDuration.Value);
                }
                else
                {
                    _remainingTime = TimeSpan.FromMinutes((int)sliderBreakDuration.Value);
                }

                UpdateTimerDisplay();
            }
        }

        #endregion

        #region Flashcards

        /// <summary>
        ///     Method responsible for adding the CardBorder click event to the cardBorder
        ///     and refreshes the list of decks and the flashcard display on the screen
        /// </summary>
        private void InitializeFlashcards()
        {
            // Add click event to the card border for flipping
            cardBorder.MouseLeftButtonDown += CardBorder_MouseLeftButtonDown;

            // Initialize UI
            RefreshDeckList();
            ResetFlashcardDisplay();
        }

        /// <summary>
        ///     Click event that handles flipping the flashcard when clicked
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void CardBorder_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            // if current deck is not null and has a card count of more than zero
            if (_currentDeck != null && _currentDeck.Cards.Count > 0)
            {
                FlipCard_Click(sender, null);
            }
        }

        /// <summary>
        ///     Click event 'AddDeck' that adds a new FlashcardDeck to the global flashcardDecks variable
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AddDeck_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtNewDeck.Text))
            {
                LogAndUpdateStatus("Error: Deck name cannot be empty", LogLevel.Error);
                return;
            }

            // Create and initialize new FlashcardDeck instance
            var newDeck = new FlashcardDeck { Name = txtNewDeck.Text.Trim() };
            _flashcardDecks.Add(newDeck);

            // Refresh list of available flashcard decks and clear the new deck text box
            RefreshDeckList();
            txtNewDeck.Clear();

            // log addition of new deck
            LogAndUpdateStatus($"Added new deck: {newDeck.Name}", LogLevel.Information);
        }

        /// <summary>
        ///     Click event 'SearchDeck' that takes a search from our search deck text box
        ///     and displays the relevant decks within the listbox.
        /// </summary>
        private void SearchDeck_Click(object sender, RoutedEventArgs e)
        {
            string searchTerm = txtSearchDeck.Text.Trim().ToLower();

            // if nothing is searched refresh deck list to show all decks
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                RefreshDeckList();
                LogAndUpdateStatus("Showing all decks", LogLevel.Information);
                return;
            }

            // variable containing the decks whos name contains the searchTerm
            var filteredDecks = from deck in _flashcardDecks
                                where deck.Name.Contains(searchTerm, StringComparison.OrdinalIgnoreCase)
                                select deck;
            // add relevant decks to the listbox
            lstDecks.ItemsSource = filteredDecks.ToList();

            // log deck search
            LogAndUpdateStatus($"Found {filteredDecks.Count()} decks matching '{searchTerm}'", LogLevel.Information);
        }

        /// <summary>
        ///     OnChange event that watches for when a deck of flashcards is selected
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DeckSelected_Changed(object sender, SelectionChangedEventArgs e)
        {

            // Gets the current selected deck in the list box as a flashcard deck
            _currentDeck = lstDecks.SelectedItem as FlashcardDeck;
            _currentCardIndex = 0;
            _isShowingFront = true;

            // if the current deck is not empty
            if (_currentDeck != null)
            {

                // update current deck label
                txtCurrentDeck.Text = _currentDeck.Name;
                txtCardCount.Text = $"{_currentDeck.Cards.Count} cards";

                // Enable study controls if there are cards
                bool hasCards = _currentDeck.Cards.Count > 0;
                btnFlipCard.IsEnabled = hasCards;
                btnNextCard.IsEnabled = hasCards;
                btnPrevCard.IsEnabled = hasCards;
                btnShuffleCards.IsEnabled = hasCards;

                // Show first card if available
                if (hasCards)
                {
                    ShowCurrentCard();
                }
                else
                {
                    txtCardContent.Text = "No cards in this deck";
                    txtClickToFlip.Visibility = Visibility.Collapsed;
                }

                // log selected deck
                LogAndUpdateStatus($"Selected deck: {_currentDeck.Name}", LogLevel.Information);
            }
            else
            {
                ResetFlashcardDisplay();
            }
        }

        /// <summary>
        /// Adds a new flashcard to the current deck if valid input is provided.
        /// Clears input fields, updates UI, enables navigation buttons if necessary, and logs the result.
        /// </summary>
        private void AddCard_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck == null)
            {
                LogAndUpdateStatus("Error: No deck selected", LogLevel.Error);
                return;
            }

            if (string.IsNullOrWhiteSpace(txtFrontSide.Text) || string.IsNullOrWhiteSpace(txtBackSide.Text))
            {
                LogAndUpdateStatus("Error: Both sides of the card must have content", LogLevel.Error);
                return;
            }

            // create new flashcard instance
            var newCard = new Flashcard
            {
                Front = txtFrontSide.Text.Trim(),
                Back = txtBackSide.Text.Trim()
            };

            _currentDeck.Cards.Add(newCard);

            // Clear the user input fields
            txtFrontSide.Clear();
            txtBackSide.Clear();

            // Update card count
            txtCardCount.Text = $"{_currentDeck.Cards.Count} cards";

            // Enable buttons if this is the first card
            if (_currentDeck.Cards.Count == 1)
            {
                btnFlipCard.IsEnabled = true;
                btnNextCard.IsEnabled = true;
                btnPrevCard.IsEnabled = true;
                btnShuffleCards.IsEnabled = true;
                ShowCurrentCard();
            }

            // log addition of card to the current deck
            LogAndUpdateStatus($"Added card to {_currentDeck.Name}", LogLevel.Information);
        }

        /// <summary>
        /// Flips the current flashcard between front and back.
        /// </summary>
        private void FlipCard_Click(object sender, RoutedEventArgs e)
        {
            // if current deck is not null and is not empty
            if (_currentDeck != null && _currentDeck.Cards.Count > 0)
            {
                // set isShowingFront to false then show the current card again
                _isShowingFront = !_isShowingFront;
                ShowCurrentCard();
            }
        }

        /// <summary>
        /// Goes to the next flashcard in the current deck, wrapping around if needed.
        /// </summary>
        private void NextCard_Click(object sender, RoutedEventArgs e)
        {
            // if current deck is not null and is not empty
            if (_currentDeck != null && _currentDeck.Cards.Count > 0)
            {
                // get the next card
                _currentCardIndex = (_currentCardIndex + 1) % _currentDeck.Cards.Count;
                _isShowingFront = true;
                ShowCurrentCard();
            }
        }
        /// <summary>
        /// Gos to the previous flashcard in the current deck, wrapping around if needed.
        /// </summary>
        private void PrevCard_Click(object sender, RoutedEventArgs e)
        {
            // if current deck is not null and is not empty
            if (_currentDeck != null && _currentDeck.Cards.Count > 0)
            {
                _currentCardIndex = (_currentCardIndex - 1 + _currentDeck.Cards.Count) % _currentDeck.Cards.Count;
                _isShowingFront = true;
                ShowCurrentCard();
            }
        }
        /// <summary>
        /// Shuffles the flashcards in the current deck using the Fisher-Yates algorithm,
        /// resets to the first card, and updates the UI.
        /// </summary>
        private void ShuffleCards_Click(object sender, RoutedEventArgs e)
        {
            if (_currentDeck != null && _currentDeck.Cards.Count > 1)
            {
                // Create a shuffled list using fisher yates shuffle algorithim
                // Create a list copy of all the cards in the current deck
                var cards = _currentDeck.Cards.ToList();
                // Initialize a random number generator
                var rng = new Random();
                // Number of cards in the deck
                int n = cards.Count;

                // Starts at the end of the deck decrementing by 1 and selects a random index between 0 and the current position
                // Swaps the card at the random index with the element at the current index
                while (n > 1)
                {
                    n--;
                    int k = rng.Next(n + 1);
                    var value = cards[k];
                    cards[k] = cards[n];
                    cards[n] = value;
                }

                // Update the collection
                _currentDeck.Cards.Clear();
                foreach (var card in cards)
                {
                    _currentDeck.Cards.Add(card);
                }

                // Reset to first card
                _currentCardIndex = 0;
                _isShowingFront = true;
                ShowCurrentCard();

                LogAndUpdateStatus($"Cards shuffled", LogLevel.Information);
            }
        }
        /// <summary>
        /// Saves all flashcard decks to a JSON file in the user's Documents folder.
        /// </summary>
        private void SaveDecks_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // gets the 'MyDocuments' folder and combines it with 'flashcards.json' to create a file path
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "flashcards.json");

                // Serializes and writes the flashcard deck into a json string to the flashcards.json file
                string json = JsonConvert.SerializeObject(_flashcardDecks, Formatting.Indented);
                File.WriteAllText(path, json);

                LogAndUpdateStatus($"Flashcards saved to: {path}", LogLevel.Information);
            }
            catch (Exception ex)
            {
                LogAndUpdateStatus($"Error saving flashcards: {ex.Message}", LogLevel.Error);
            }
        }
        /// <summary>
        /// Loads flashcard decks from a JSON file in the user's Documents folder.
        /// Updates the deck list and resets the UI.
        /// </summary>
        private void LoadDecks_Click(object sender, RoutedEventArgs e)
        {
            try
            {   
                // gets the 'MyDocuments' folder and combines it with 'flashcards.json' to create a file path
                string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "flashcards.json");

                // if the file exists at the path defined
                if (File.Exists(path))
                {
                    // Read all the text from the json file and convert the json into the FlashcardDeck as a Observable Collection
                    string json = File.ReadAllText(path);
                    var loadedDecks = JsonConvert.DeserializeObject<ObservableCollection<FlashcardDeck>>(json);

                    if (loadedDecks != null)
                    {
                        // Load in the decks and refresh the deck list box and reset the current flashcard display
                        _flashcardDecks = loadedDecks;
                        RefreshDeckList();
                        ResetFlashcardDisplay();

                        LogAndUpdateStatus("Flashcards loaded from file", LogLevel.Information);
                    }
                }
                else
                {
                    LogAndUpdateStatus("No saved flashcards found", LogLevel.Warning);
                }
            }
            catch (Exception ex)
            {
                LogAndUpdateStatus($"Error loading flashcards: {ex.Message}", LogLevel.Error);
            }
        }

        /// <summary>
        /// Displays the current flashcard content based on showing front or back state.
        /// Also updates UI colors and visibility.
        /// </summary>
        private void ShowCurrentCard()
        {
            // if current deck is not null and is not empty
            if (_currentDeck != null && _currentDeck.Cards.Count > 0)
            {
                // get current card by index and update ui elements
                var currentCard = _currentDeck.Cards[_currentCardIndex];
                txtCardContent.Text = _isShowingFront ? currentCard.Front : currentCard.Back;
                txtClickToFlip.Visibility = Visibility.Visible;

                // Update card display
                cardBorder.Background = _isShowingFront ? Brushes.LightYellow : Brushes.LightBlue;
            }
        }
        /// <summary>
        /// Refreshes the list of available flashcard decks.
        /// </summary>
        private void RefreshDeckList()
        {
            lstDecks.ItemsSource = null;
            lstDecks.ItemsSource = _flashcardDecks;
        }
        /// <summary>
        /// Clears the flashcard display and disables interaction buttons.
        /// Called when no deck is selected or decks are reloaded.
        /// </summary>
        private void ResetFlashcardDisplay()
        {
            txtCurrentDeck.Text = "None selected";
            txtCardCount.Text = "0 cards";
            txtCardContent.Text = "Select a deck to study";
            txtClickToFlip.Visibility = Visibility.Collapsed;

            btnFlipCard.IsEnabled = false;
            btnNextCard.IsEnabled = false;
            btnPrevCard.IsEnabled = false;
            btnShuffleCards.IsEnabled = false;
        }

        #endregion

        /// <summary>
        /// Method that updates the UI status text and logs the message using the _logger.
        /// </summary>
        /// <param name="message">The message to display and log.</param>
        /// <param name="logLevel">The severity level of the log.</param>
        private void LogAndUpdateStatus(string message, LogLevel logLevel)
        {
            // Update UI status text
            txtStatus.Text = $"{DateTime.Now:T} - {message}";

            // Log to file with appropriate level
            switch (logLevel)
            {
                case LogLevel.Error:
                    _logger.LogError(message);
                    break;
                case LogLevel.Warning:
                    _logger.LogWarning(message);
                    break;
                case LogLevel.Information:
                default:
                    _logger.LogInformation(message);
                    break;
            }
        }
    }
}