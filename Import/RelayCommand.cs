using System;
using System.Windows.Input;

namespace PlayniteCustomImporter.Import
{
    /// <summary>
    /// Minimal ICommand implementation for MVVM bindings.
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T> execute;
        private readonly Predicate<T> canExecute;

        public RelayCommand(Action<T> execute, Predicate<T> canExecute = null)
        {
            this.execute = execute ?? throw new ArgumentNullException(nameof(execute));
            this.canExecute = canExecute;
        }

        public bool CanExecute(object parameter)
        {
            return canExecute == null || canExecute(parameter is T t ? t : default(T));
        }

        public void Execute(object parameter)
        {
            execute(parameter is T t ? t : default(T));
        }

        public event EventHandler CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }
    }
}
