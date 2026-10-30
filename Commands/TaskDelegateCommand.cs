
using System.Windows.Input;
using System.Linq.Expressions;


namespace CertTool.Commands
{
    public class TaskDelegateCommand<T> : DelegateCommandBase, ICommand
    {
        private readonly Func<T, Task> _task;
        private Func<T, bool>? _canExecute;
        public bool IsExecuting { get; set; }

        public TaskDelegateCommand(Func<T, Task> task)
        {
            _task = task;
        }

        public TaskDelegateCommand(Func<T, Task> task, Func<T, bool> canExecute)
        {
            _task = task;
            _canExecute = canExecute;
        }


        protected override bool CanExecute(object? parameter)
        {

            //Implement is executing fra base class
            if (_canExecute == null)
            {
                return true;
            }

            return parameter is T p && _canExecute(p);
        }

        protected override async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
        }

        protected async Task ExecuteAsync(object? parameter)
        {

            try
            {
                IsExecuting = true;
                this.RaiseCanExecuteChanged();

                await _task(parameter is T p ? p : default);

            }
            finally
            {
                IsExecuting = false;
                this.RaiseCanExecuteChanged();
            }
        }

        public TaskDelegateCommand<T> ObservesProperty<TType>(Expression<Func<TType>> propertyExpression)
        {
            ObservesPropertyInternal(propertyExpression);
            return this;
        }

        /// <summary>
        /// Observes a property that is used to determine if this command can execute, and if it implements INotifyPropertyChanged it will automatically call DelegateCommandBase.RaiseCanExecuteChanged on property changed notifications.
        /// </summary>
        /// <param name="canExecuteExpression">The property expression. Example: ObservesCanExecute(() => PropertyName).</param>
        /// <returns>The current instance of DelegateCommand</returns>
        public TaskDelegateCommand<T> ObservesCanExecute(Expression<Func<bool>> canExecuteExpression)
        {
            Expression<Func<T, bool>> expression = Expression.Lambda<Func<T, bool>>(canExecuteExpression.Body, Expression.Parameter(typeof(T), "o"));
            _canExecute = expression.Compile();
            ObservesPropertyInternal(canExecuteExpression);
            return this;
        }

    }


    public class TaskDelegateCommand : DelegateCommandBase, ICommand
    {
        private readonly Func<Task> _task;
        private Func<bool>? _canExecute;
        public bool IsExecuting { get; set; }


        public TaskDelegateCommand(Func<Task> task)
        {
            _task = task;
        }

        public TaskDelegateCommand(Func<Task> task, Func<bool> canExecute)
        {
            _task = task;
            _canExecute = canExecute;
        }

        protected override bool CanExecute(object? parameter)
        {

            if (_canExecute == null)
            {
                return true;
            }

            return _canExecute();
        }

        protected override async void Execute(object? parameter)
        {
            await ExecuteAsync(parameter);
        }

        protected async Task ExecuteAsync(object? parameter)
        {

            try
            {
                IsExecuting = true;
                this.RaiseCanExecuteChanged();

                await _task();

            }
            finally
            {
                IsExecuting = false;
                this.RaiseCanExecuteChanged();
            }
        }

        public TaskDelegateCommand ObservesProperty<T>(Expression<Func<T>> propertyExpression)
        {
            ObservesPropertyInternal(propertyExpression);
            return this;
        }

        /// <summary>
        /// Observes a property that is used to determine if this command can execute, and if it implements INotifyPropertyChanged it will automatically call DelegateCommandBase.RaiseCanExecuteChanged on property changed notifications.
        /// </summary>
        /// <param name="canExecuteExpression">The property expression. Example: ObservesCanExecute(() => PropertyName).</param>
        /// <returns>The current instance of DelegateCommand</returns>
        public TaskDelegateCommand ObservesCanExecute(Expression<Func<bool>> canExecuteExpression)
        {
            _canExecute = canExecuteExpression.Compile();
            ObservesPropertyInternal(canExecuteExpression);
            return this;
        }
    }
}


