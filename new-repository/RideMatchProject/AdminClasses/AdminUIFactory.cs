using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.AdminClasses
{
    /// <summary>
    /// Factory for creating UI elements
    /// </summary>
    public static class AdminUIFactory
    {
        public static Button CreateButton(
            string text,
            Point location,
            Size size,
            EventHandler clickHandler)
        {
            var button = new Button
            {
                Text = text,
                Location = location,
                Size = size
            };

            if (clickHandler != null)
            {
                button.Click += clickHandler;
            }

            return button;
        }

        public static Label CreateLabel(
            string text,
            Point location,
            Size size,
            ContentAlignment textAlign = ContentAlignment.MiddleLeft)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                TextAlign = textAlign
            };

            return label;
        }

        public static TextBox CreateTextBox(
            Point location,
            Size size,
            string text = "",
            bool multiline = false)
        {
            return new TextBox
            {
                Location = location,
                Size = size,
                Text = text,
                Multiline = multiline
            };
        }

        public static CheckBox CreateCheckBox(
            string text,
            Point location,
            Size size,
            bool isChecked = false)
        {
            return new CheckBox
            {
                Text = text,
                Location = location,
                Size = size,
                Checked = isChecked
            };
        }

        public static Panel CreatePanel(
            Point location,
            Size size,
            BorderStyle borderStyle = BorderStyle.None)
        {
            return new Panel
            {
                Location = location,
                Size = size,
                BorderStyle = borderStyle
            };
        }
    }

}
