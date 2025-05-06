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
    /// A static factory class for creating standard UI elements used in the admin interface.
    /// This improves code readability, consistency, and reusability.
    /// </summary>
    public static class AdminUIFactory
    {
        /// <summary>
        /// Creates a standard Windows Forms Button with specified properties.
        /// </summary>
        /// <param name="text">Text displayed on the button.</param>
        /// <param name="location">Location of the button on the form.</param>
        /// <param name="size">Size of the button.</param>
        /// <param name="clickHandler">Event handler for click events. Can be null.</param>
        /// <returns>A configured Button object.</returns>
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

        /// <summary>
        /// Creates a Label with the specified text, position, and alignment.
        /// </summary>
        /// <param name="text">Text displayed in the label.</param>
        /// <param name="location">Location of the label on the form.</param>
        /// <param name="size">Size of the label.</param>
        /// <param name="textAlign">Alignment of the text. Default is MiddleLeft.</param>
        /// <returns>A configured Label object.</returns>
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

        /// <summary>
        /// Creates a TextBox for input, supporting optional initial text and multiline mode.
        /// </summary>
        /// <param name="location">Location of the textbox on the form.</param>
        /// <param name="size">Size of the textbox.</param>
        /// <param name="text">Initial text inside the textbox (optional).</param>
        /// <param name="multiline">Whether the textbox allows multiple lines. Default is false.</param>
        /// <returns>A configured TextBox object.</returns>
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

        /// <summary>
        /// Creates a CheckBox with optional default checked state.
        /// </summary>
        /// <param name="text">Text label shown next to the checkbox.</param>
        /// <param name="location">Location of the checkbox on the form.</param>
        /// <param name="size">Size of the checkbox.</param>
        /// <param name="isChecked">Initial checked state. Default is false.</param>
        /// <returns>A configured CheckBox object.</returns>
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

        /// <summary>
        /// Creates a Panel, which can be used as a container for grouping controls.
        /// </summary>
        /// <param name="location">Location of the panel on the form.</param>
        /// <param name="size">Size of the panel.</param>
        /// <param name="borderStyle">Optional border style. Default is None.</param>
        /// <returns>A configured Panel object.</returns>
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
