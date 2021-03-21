using Carnassial.Command;
using Carnassial.Dialog;
using Carnassial.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;

namespace Carnassial.UnitTests
{
    [TestClass]
    public class LowLevelTests : CarnassialTest
    {
        [ClassCleanup]
        public static void ClassCleanup()
        {
            CarnassialTest.TryRevertToDefaultCultures();
        }

        [ClassInitialize]
        public static void ClassInitialize(TestContext testContext)
        {
            CarnassialTest.TryChangeToTestCulture();
        }

        /// <summary>
        /// Basic functional validation of <see cref="MostRecentlyUsedList" />.
        /// </summary>
        [TestMethod]
        public void MostRecentlyUsedList()
        {
            MostRecentlyUsedList<int> mruList = new(5);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out int mostRecent));
            Assert.IsTrue(mostRecent == 0);
            List<int> list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 0);

            mruList.SetMostRecent(1);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 1);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 1);
            Assert.IsTrue(list[1] == 0);

            mruList.SetMostRecent(0);
            Assert.IsFalse(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 0);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 2);
            Assert.IsTrue(list[0] == 0);
            Assert.IsTrue(list[1] == 1);

            Assert.IsTrue(mruList.TryRemove(0));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 1);
            Assert.IsTrue(list[0] == 1);

            Assert.IsFalse(mruList.TryRemove(0));
            Assert.IsTrue(mruList.TryRemove(1));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 0);

            mruList.SetMostRecent(2);
            mruList.SetMostRecent(3);
            mruList.SetMostRecent(4);
            mruList.SetMostRecent(5);
            mruList.SetMostRecent(6);
            mruList.SetMostRecent(7);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 7);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 7);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(6);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 6);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 6);
            Assert.IsTrue(list[1] == 7);
            Assert.IsTrue(list[2] == 5);
            Assert.IsTrue(list[3] == 4);
            Assert.IsTrue(list[4] == 3);

            mruList.SetMostRecent(3);
            Assert.IsTrue(mruList.IsFull());
            Assert.IsTrue(mruList.TryGetMostRecent(out mostRecent));
            Assert.IsTrue(mostRecent == 3);
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 5);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 5);
            Assert.IsTrue(list[4] == 4);

            Assert.IsFalse(mruList.TryRemove(-1));
            Assert.IsTrue(mruList.IsFull());

            Assert.IsTrue(mruList.TryRemove(5));
            Assert.IsFalse(mruList.IsFull());
            list = mruList.ToList();
            Assert.IsTrue(list.Count == 4);
            Assert.IsTrue(list[0] == 3);
            Assert.IsTrue(list[1] == 6);
            Assert.IsTrue(list[2] == 7);
            Assert.IsTrue(list[3] == 4);

            Assert.IsTrue(mruList.TryGetLeastRecent(out int leastRecent));
            Assert.IsTrue(leastRecent == 4);
        }

        [TestMethod]
        public void ResourceKeys()
        {
            foreach (FieldInfo field in typeof(Constant.ResourceKey).GetFields(BindingFlags.Public | BindingFlags.Static))
            {
                string resourceKey = (string)field.GetValue(null);
                if (String.Equals(resourceKey, Constant.ResourceKey.ApplicationWindowException, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowClockDriftFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowCopyFileFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowDatabaseLoadFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowDaylightSavingsFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowFileMoveIncomplete, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowExportSpreadsheetFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowImageMetadataFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowImport, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowImportFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowImportIncomplete, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowNoAmbiguousDates, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowNoDeletableFiles, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowNoMetadataAvailable, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowSelectFolder, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.CarnassialWindowTemplateLoadFailed, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DataEntryHandlerConfirmCopyAll, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DataEntryHandlerConfirmCopyForward, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DataEntryHandlerConfirmPropagateToHere, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DataEntryHandlerNothingToCopyForward, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DataEntryHandlerNothingToPropagate, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DeleteFilesMessageCurrentFileAndData, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DeleteFilesMessageCurrentFileOnly, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DeleteFilesMessageFilesAndData, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.DeleteFilesMessageFilesOnly, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.GithubReleaseClientGetNewVersion, StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(resourceKey, Constant.ResourceKey.GithubReleaseClientNoUpdates, StringComparison.OrdinalIgnoreCase))
                {
                    Message message = App.FindResource<Message>(resourceKey);
                    Assert.IsTrue(message != null);
                }
                else if (String.Equals(resourceKey, Constant.ResourceKey.AboutTermsOfUse, StringComparison.OrdinalIgnoreCase))
                {
                    Span span = App.FindResource<Span>(resourceKey);
                    Assert.IsTrue(span != null);
                }
                else if (String.Equals(resourceKey, Constant.ResourceKey.SearchTermListCellMargin, StringComparison.OrdinalIgnoreCase))
                {
                    Thickness thickness = App.FindResource<Thickness>(resourceKey);
                    Assert.IsTrue((thickness.Top > 0.0) && (thickness.Right > 0.0) && (thickness.Bottom > 0.0) && (thickness.Left > 0.0));
                }
                else
                {
                    // strings
                    string resource = App.FindResource<string>(resourceKey);
                    Assert.IsTrue(String.IsNullOrWhiteSpace(resource) == false);
                }
            }
        }

        /// <summary>
        /// Basic functional validation of <see cref="UndoRedoChain" />.
        /// </summary>
        [TestMethod]
        public void UndoRedoChain()
        {
            // zero states
            UndoRedoChain<int> undoRedoChain = new();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out UndoableCommand<int> state));
            Assert.IsFalse(undoRedoChain.TryMoveToNextUndo(out state));

            // basic lifecycle with two different states
            undoRedoChain.AddCommand(new TestCommand(0));
            undoRedoChain.AddCommand(new TestCommand(1));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextRedo(out state));
            state.Execute(0);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextRedo(out state));
            state.Execute(0);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out state));
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);

            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);

            undoRedoChain.Clear();
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsFalse(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out state));
            Assert.IsFalse(undoRedoChain.TryMoveToNextUndo(out state));

            // one state
            TestCommand firstState = new(3);
            undoRedoChain.AddCommand(firstState);
            Assert.IsFalse(undoRedoChain.CanRedo);
            Assert.IsTrue(undoRedoChain.CanUndo);
            Assert.IsFalse(undoRedoChain.TryMoveToNextRedo(out state));
            Assert.IsTrue(undoRedoChain.TryMoveToNextUndo(out state));
            state.Undo(0);
            Assert.IsTrue(firstState.Equals(state));
        }

        internal class TestCommand : UndoableCommand<int>
        {
            public int ID { get; private set; }

            public TestCommand(int id)
            {
                this.ID = id;
                this.IsExecuted = true;
            }

            public override void Execute(int parameter)
            {
                this.IsExecuted = true;
            }

            public override void Undo(int parameter)
            {
                this.IsExecuted = false;
            }
        }
    }
}
