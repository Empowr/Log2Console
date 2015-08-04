﻿using System;
using System.Collections.Generic;
using System.Windows.Forms;
using Log2Console.Receiver;

namespace Log2Console.Settings
{
    public partial class ReceiversForm : Form
    {
        public ReceiversForm(IEnumerable<IReceiver> receivers)
        {
            AddedReceivers = new List<IReceiver>();
            RemovedReceivers = new List<IReceiver>();

            InitializeComponent();

            Font = UserSettings.Instance.DefaultFont ?? Font;

            // Populate Receiver Types
            var receiverTypes = ReceiverFactory.Instance.ReceiverTypes;
            foreach (var kvp in receiverTypes)
            {
                var item = addReceiverCombo.DropDownItems.Add(kvp.Value.Name);
                item.Tag = kvp.Value;
            }

            // Populate Existing Receivers
            foreach (var receiver in receivers)
                AddReceiver(receiver);
        }

        public List<IReceiver> AddedReceivers { get; protected set; }
        public List<IReceiver> RemovedReceivers { get; protected set; }

        private void AddReceiver(IReceiver receiver)
        {
            var displayName = string.IsNullOrEmpty(receiver.DisplayName)
                ? ReceiverUtils.GetTypeDescription(receiver.GetType())
                : receiver.DisplayName;
            var lvi = receiversListView.Items.Add(displayName);
            lvi.Tag = receiver;
            lvi.Selected = true;
        }

        private void addReceiverCombo_DropDownItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            var info = e.ClickedItem.Tag as ReceiverFactory.ReceiverInfo;
            if (info != null)
            {
                // Instantiates a new receiver based on the selected type
                var receiver = ReceiverFactory.Instance.Create(info.Type.FullName);

                AddedReceivers.Add(receiver);
                AddReceiver(receiver);
            }
        }

        private void removeReceiverBtn_Click(object sender, EventArgs e)
        {
            var receiver = GetSelectedReceiver();
            if (receiver == null)
                return;

            var dr = MessageBox.Show(this, "Confirm Delete?", "Confirmation", MessageBoxButtons.YesNo,
                MessageBoxIcon.Question, MessageBoxDefaultButton.Button2);
            if (dr != DialogResult.Yes)
                return;

            receiversListView.Items.Remove(GetSelectedItem());

            if (AddedReceivers.Find(r => r == receiver) != null)
                AddedReceivers.Remove(receiver);
            else
                RemovedReceivers.Add(receiver);
        }

        private void receiversListView_SelectedIndexChanged(object sender, EventArgs e)
        {
            var receiver = GetSelectedReceiver();

            removeReceiverBtn.Enabled = (receiver != null);
            receiverPropertyGrid.SelectedObject = receiver;

            if (receiver != null)
                sampleClientConfigTextBox.Text = receiver.SampleClientConfig;
        }

        private ListViewItem GetSelectedItem()
        {
            if (receiversListView.SelectedItems.Count > 0)
                return receiversListView.SelectedItems[0];
            return null;
        }

        private IReceiver GetSelectedReceiver()
        {
            if (receiversListView.SelectedItems.Count <= 0)
                return null;

            var lvi = GetSelectedItem();
            return (lvi == null) ? null : lvi.Tag as IReceiver;
        }
    }
}