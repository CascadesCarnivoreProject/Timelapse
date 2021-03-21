using System;
using System.Windows.Input;

namespace Carnassial.Command
{
    public abstract class UndoableCommand<TParameter> : ICommand
    {
        private bool isExecuted;

        public event EventHandler? CanExecuteChanged;

        public UndoableCommand()
        {
            this.isExecuted = false;
        }

        public bool IsExecuted
        {
            get
            {
                return this.isExecuted;
            }

            protected set
            {
                if (value != this.isExecuted)
                {
                    this.isExecuted = value;
                    this.CanExecuteChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        public virtual bool IsAsync
        {
            get { return false; }
        }

        public virtual bool CanExecute(TParameter parameter)
        {
            return this.IsExecuted == false;
        }

        public virtual bool CanUndo(TParameter parameter)
        {
            return this.IsExecuted;
        }

        public abstract void Execute(TParameter parameter);

        bool ICommand.CanExecute(object? parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }
            return this.CanExecute((TParameter)parameter);
        }

        void ICommand.Execute(object? parameter)
        {
            if (parameter == null)
            {
                throw new ArgumentNullException(nameof(parameter));
            }
            this.Execute((TParameter)parameter);
        }

        public abstract void Undo(TParameter parameter);
    }
}
