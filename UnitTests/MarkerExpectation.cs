﻿using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Collections.Generic;
using System.Data;
using Timelapse.Database;

namespace Timelapse.UnitTests
{
    internal class MarkerExpectation
    {
        public MarkerExpectation()
        {
            this.UserDefinedCountersByDataLabel = new Dictionary<string, string>();
        }

        public long ID { get; set; }

        public Dictionary<string, string> UserDefinedCountersByDataLabel { get; private set; }

        public void Verify(DataRow marker)
        {
            Assert.IsTrue(marker.GetID() == this.ID, "{0}: Expected ID '{1}' but found '{2}'.", this.ID, this.ID, marker.GetID());

            foreach (KeyValuePair<string, string> userCounterExpectation in this.UserDefinedCountersByDataLabel)
            {
                Assert.IsTrue(marker.GetStringField(userCounterExpectation.Key) == userCounterExpectation.Value, "{0}: Expected {1} to be '{2}' but found '{3}'.", this.ID, userCounterExpectation.Key, userCounterExpectation.Value, marker.GetStringField(userCounterExpectation.Key));
            }
        }
    }
}
