using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.LoginClasses
{
    public static class LoginControlFactory
    {
        public static Label CreateLabel(
            string text,
            Point location,
            Size size,
            Font font = null,
            ContentAlignment textAlign = ContentAlignment.MiddleLeft)
        {
            var label = new Label
            {
                Text = text,
                Location = location,
                Size = size,
                TextAlign = textAlign
            };

            if (font != null)
            {
                label.Font = font;
            }

            return label;
        }

        public static TextBox CreateTextBox(Point location, Size size)
        {
            return new TextBox
            {
                Location = location,
                Size = size
            };
        }

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

        public static ComboBox CreateComboBox(
            Point location,
            Size size,
            string[] items,
            int selectedIndex)
        {
            var comboBox = new ComboBox
            {
                Location = location,
                Size = size,
                DropDownStyle = ComboBoxStyle.DropDownList
            };

            if (items != null)
            {
                comboBox.Items.AddRange(items);
            }

            if (selectedIndex >= 0 && selectedIndex < comboBox.Items.Count)
            {
                comboBox.SelectedIndex = selectedIndex;
            }

            return comboBox;
        }
    }
}
