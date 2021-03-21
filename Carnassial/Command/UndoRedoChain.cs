using System.Collections.Generic;
using System.Diagnostics;

namespace Carnassial.Command
{
    public class UndoRedoChain<TParameter>
    {
        private readonly LinkedList<UndoableCommand<TParameter>> chain;
        private LinkedListNode<UndoableCommand<TParameter>>? position;

        public UndoRedoChain()
        {
            this.chain = new LinkedList<UndoableCommand<TParameter>>();
            this.position = null;
        }

        public bool CanRedo
        {
            get { return (this.position != null) && ((this.position.Value.IsExecuted == false) || this.position.Next != null); }
        }

        public bool CanUndo
        {
            // when a single state is available a redo will be inserted on calling GetUndo()
            // In this case this.position's not decremented, so a position of 0 indicates undo availability.
            get { return (this.position != null) && (this.position.Value.IsExecuted || (this.position.Previous != null)); }
        }

        private LinkedListNode<UndoableCommand<TParameter>> AddLast(UndoableCommand<TParameter> state)
        {
            // add
            LinkedListNode<UndoableCommand<TParameter>> last = this.chain.AddLast(state);

            // limit memory footprint of chain
            if (this.chain.Count > Constant.MaximumUndoableCommands)
            {
                this.chain.RemoveFirst();
            }

            return last;
        }

        public void AddCommand(UndoableCommand<TParameter> state)
        {
            // remove any available redo chain as it's invalidated by the incoming undo
            if (this.CanRedo)
            {
                for (LinkedListNode<UndoableCommand<TParameter>>? nodeToRemove = this.chain.Last; nodeToRemove != this.position; nodeToRemove = this.chain.Last)
                {
                    this.chain.RemoveLast();
                }
                Debug.Assert(this.position != null);
                if (this.position.Value.IsExecuted == false)
                {
                    this.chain.RemoveLast();
                }
            }

            // add state
            this.position = this.AddLast(state);
        }

        public void Clear()
        {
            this.chain.Clear();
            this.position = null;
        }

        public bool TryMoveToNextRedo(out UndoableCommand<TParameter>? state)
        {
            if (this.CanRedo == false)
            {
                state = null;
                return false;
            }

            Debug.Assert(this.position != null);
            if (this.position.Value.IsExecuted)
            {
                this.position = this.position.Next;
                Debug.Assert((this.position != null) && (this.position.Value.IsExecuted == false), "Redid to a command which has been executed.");
            }
            state = this.position?.Value;
            return true;
        }

        public bool TryMoveToNextUndo(out UndoableCommand<TParameter>? state)
        {
            if (this.CanUndo == false)
            {
                state = null;
                return false;
            }

            Debug.Assert(this.position != null);
            if (this.position.Value.IsExecuted == false)
            {
                this.position = this.position.Previous;
                Debug.Assert((this.position != null) && this.position.Value.IsExecuted, "Undid to a command which hasn't been executed.");
            }
            state = this.position.Value;
            return true;
        }
    }
}
