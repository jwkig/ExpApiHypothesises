using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Xml.Serialization;
using Autofac;
using Autofac.Core;
using Autofac.Core.Lifetime;
using EventBus;
using EventBus.Abstractions;
using EventBus.Events;
using EventBusRabbitMQ;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;


namespace WpfClient
{

    public interface ILogger
    {
        void AppendLog(string message);
        void ClearLog();
    }

    public class Button1ClickEvent : IntegrationEvent
    {

    }

    public class Button2ClickEvent : IntegrationEvent
    {

    }

    public class Button1ClickEventHandler : IIntegrationEventHandler<Button1ClickEvent>
    {
        private ILogger _logger;

        public Button1ClickEventHandler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Implementation of IIntegrationEventHandler<in Button1ClickEvent>
        public async Task Handle(Button1ClickEvent @event)
        {
            await Task.Run(() => _logger?.AppendLog($"Received {@event.GetType()} event"));
        }

        #endregion
    }

    public class Button2ClickEventHandler : IIntegrationEventHandler<Button2ClickEvent>
    {
        private ILogger _logger;

        public Button2ClickEventHandler(ILogger logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        #region Implementation of IIntegrationEventHandler<in Button1ClickEvent>
        public async Task Handle(Button2ClickEvent @event)
        {
            await Task.Run(() => _logger?.AppendLog($"Received {@event.GetType()} event"));
        }

        #endregion
    }

    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, ILogger
    {
        private ILogger<DefaultRabbitMQPersistentConnection> _connectionLogger;
        private IRabbitMQPersistentConnection _connection;
        private IEventBus _eventBus;
        private ILogger<EventBusRabbitMQ.EventBusRabbitMQ> _eventBusLogger;
        private IEventBusSubscriptionsManager _subscriptionsManager;
        private ILifetimeScope _autofac;
        private StringBuilder _log = new StringBuilder();
        private bool _autoScroll = true;
        public string SubscriptionClientName { get; set; }
        public static readonly DependencyProperty LogProperty;
        public static readonly DependencyProperty ConnectCaptionProperty;
        public static readonly DependencyProperty IsConnectedProperty;

        public string ConnectCaption
        {
            get { return (string) GetValue(ConnectCaptionProperty); }
            set { SetValue(ConnectCaptionProperty, value); }
        }

        public string Log
        {
            get { return (string)GetValue(LogProperty); }
            set { SetValue(LogProperty, value); }
        }

        public bool IsConnected
        {
            get { return (bool)GetValue(IsConnectedProperty); }
            set
            {
                SetValue(IsConnectedProperty, value); 
                ConnectCaption = value ? "Отсоединиться" : "Соединиться";
            }
        }

        public void AppendLog(string message)
        {
            Dispatcher.Invoke(() =>
            {
                _log.AppendLine($"{DateTime.Now.ToString("dd.MM.yyyy hh:mm:ss:fff")} - {message}");
                Log = _log.ToString();
            });
        }

        public void ClearLog()
        {
            Dispatcher.Invoke(() =>
            {
                _log.Clear();
                Log = _log.ToString();
            });
        }

        public void ConnectEventBus()
        {
            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                DispatchConsumersAsync = true
            };
            _connectionLogger = new Mock<ILogger<DefaultRabbitMQPersistentConnection>>().Object;
            var retryCount = 5;
            _connection = new DefaultRabbitMQPersistentConnection(factory, _connectionLogger, retryCount);
            
            var builder = new ContainerBuilder();
            
            var container = builder.Build();

            _autofac = container.BeginLifetimeScope(builder =>
            {
                builder.RegisterType<Button1ClickEventHandler>().UsingConstructor(typeof(ILogger));
                builder.RegisterType<Button2ClickEventHandler>().UsingConstructor(typeof(ILogger));
                builder.RegisterInstance(this).As<ILogger>();
            });
            _eventBusLogger = new Mock<ILogger<EventBusRabbitMQ.EventBusRabbitMQ>>().Object;
            var eventBusSubcriptionsManager = new InMemoryEventBusSubscriptionsManager();
            _eventBus = new EventBusRabbitMQ.EventBusRabbitMQ(_connection, _eventBusLogger, _autofac, eventBusSubcriptionsManager, SubscriptionClientName, retryCount);
            IsConnected = true;
            AppendLog("Connected");
        }

        public void DisconnectEventBus()
        {
            try
            {
                if (IsConnected)
                {
                    (_eventBus as IDisposable)?.Dispose();
                    _autofac?.Dispose();
                    _connection?.Dispose();
                    AppendLog("Disconnected");
                }
            }
            finally
            {
                IsConnected = false;
            }
        }

        static MainWindow()
        {
            LogProperty = DependencyProperty.Register(
                nameof(Log),
                typeof(string),
                typeof(MainWindow));
            ConnectCaptionProperty = DependencyProperty.Register(
                nameof(ConnectCaption),
                typeof(string),
                typeof(MainWindow));
            IsConnectedProperty = DependencyProperty.Register(
                nameof(IsConnected),
                typeof(bool),
                typeof(MainWindow));

        }
        public MainWindow()
        {
            SubscriptionClientName = $"WpfClient-{Guid.NewGuid()}";
            InitializeComponent();
            ClearLog();
            IsConnected = false;
            Closed += (sender, args) => DisconnectEventBus();
        }

        private void ConnectClick(object sender, RoutedEventArgs e)
        {
            if (IsConnected)
            {
                DisconnectEventBus();
            }
            else
            {
                ConnectEventBus();
            }
        }

        private void OnButton1Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Publish(new Button1ClickEvent());
                    AppendLog($"Published event '{nameof(Button1ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnButton2Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Publish(new Button2ClickEvent());
                    AppendLog($"Published event '{nameof(Button2ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnSubscribe1Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Subscribe<Button1ClickEvent, Button1ClickEventHandler>();
                    AppendLog($"Subscribed on event '{nameof(Button1ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnSubscribe2Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Subscribe<Button2ClickEvent, Button2ClickEventHandler>();
                    AppendLog($"Subscribed on event '{nameof(Button2ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnUnsubscribe1Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Unsubscribe<Button1ClickEvent, Button1ClickEventHandler>();
                    AppendLog($"Unsubscribed on event '{nameof(Button1ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnUnsubscribe2Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (IsConnected)
                {
                    _eventBus.Unsubscribe<Button2ClickEvent, Button2ClickEventHandler>();
                    AppendLog($"Unsubscribed on event '{nameof(Button2ClickEvent)}'");
                }
            }
            catch (Exception ex)
            {
                AppendLog(ex.ToString());
            }
        }

        private void OnClearLogBtnClick(object sender, RoutedEventArgs e)
        {
            ClearLog();
        }
        private void OnScrollViewerScrollChanged(Object sender, ScrollChangedEventArgs e)
        {
            if (e.ExtentHeightChange == 0)
            {
                _autoScroll = ScrollViewer.VerticalOffset == ScrollViewer.ScrollableHeight;
            }

            if (_autoScroll && e.ExtentHeightChange != 0)
            {
                ScrollViewer.ScrollToVerticalOffset(ScrollViewer.ExtentHeight);
            }
        }
    }
}
