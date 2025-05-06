using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace RideMatchProject.LoginClasses
{
    /// <summary>
    /// A static factory class for creating Windows Forms controls used in login or registration forms.
    /// Provides methods to create pre-configured <see cref="Label"/>, <see cref="TextBox"/>, <see cref="Button"/>, and <see cref="ComboBox"/> controls.
    /// </summary>
    public static class LoginControlFactory
    {
        /// <summary>
        /// Creates a configured <see cref="Label"/> control with specified text, location, size, and optional font and alignment.
        /// </summary>
        /// <param name="text">The text to display on the label.</param>
        /// <param name="location">The <see cref="Point"/> specifying the top-left position of the label on the form.</param>
        /// <param name="size">The <see cref="Size"/> specifying the width and height of the label.</param>
        /// <param name="font">The <see cref="Font"/> to use for the label text. If null, the default system font is used. Defaults to null.</param>
        /// <param name="textAlign">The <see cref="ContentAlignment"/> for the label text. Defaults to <see cref="ContentAlignment.MiddleLeft"/>.</param>
        /// <returns>A configured <see cref="Label"/> control with the specified properties.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="size"/> has negative or zero width/height.</exception>
        public static Label CreateLabel(
            string text,
            Point location,
            Size size,
            Font font = null,
            ContentAlignment textAlign = ContentAlignment.MiddleLeft)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Label text cannot be null.");

            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Size must have positive width and height.", nameof(size));

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

        /// <summary>
        /// Creates a configured <see cref="TextBox"/> control with specified location and size.
        /// </summary>
        /// <param name="location">The <see cref="Point"/> specifying the top-left position of the text box on the form.</param>
        /// <param name="size">The <see cref="Size"/> specifying the width and height of the text box.</param>
        /// <returns>A configured <see cref="TextBox"/> control with the specified properties.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="size"/> has negative or zero width/height.</exception>
        public static TextBox CreateTextBox(Point location, Size size)
        {
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Size must have positive width and height.", nameof(size));

            return new TextBox
            {
                Location = location,
                Size = size
            };
        }

        /// <summary>
        /// Creates a configured <see cref="Button"/> control with specified text, location, size, and optional click event handler.
        /// </summary>
        /// <param name="text">The text to display on the button.</param>
        /// <param name="location">The <see cref="Point"/> specifying the top-left position of the button on the form.</param>
        /// <param name="size">The <see cref="Size"/> specifying the width and height of the button.</param>
        /// <param name="clickHandler">The <see cref="EventHandler"/> to invoke when the button is clicked. If null, no handler is attached.</param>
        /// <returns>A configured <see cref="Button"/> control with the specified properties.</returns>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="text"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="size"/> has negative or zero width/height.</exception>
        public static Button CreateButton(
            string text,
            Point location,
            Size size,
            EventHandler clickHandler)
        {
            if (text == null)
                throw new ArgumentNullException(nameof(text), "Button text cannot be null.");

            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Size must have positive width and height.", nameof(size));

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
        /// Creates a configured <see cref="ComboBox"/> control with specified location, size, items, and selected index.
        /// The ComboBox is set to <see cref="ComboBoxStyle.DropDownList"/> style, allowing only selection from the provided items.
        /// </summary>
        /// <param name="location">The <see cref="Point"/> specifying the top-left position of the ComboBox on the form.</param>
        /// <param name="size">The <see cref="Size"/> specifying the width and height of the ComboBox.</param>
        /// <param name="items">An array of strings to populate the ComboBox items. If null, the ComboBox remains empty.</param>
        /// <param name="selectedIndex">The zero-based index of the item to select initially. If negative or out of range, no item is selected.</param>
        /// <returns>A configured <see cref="ComboBox"/> control with the specified properties.</returns>
        /// <exception cref="ArgumentException">Thrown when <paramref name="size"/> has negative or zero width/height.</exception>
        public static ComboBox CreateComboBox(
            Point location,
            Size size,
            string[] items,
            int selectedIndex)
        {
            if (size.Width <= 0 || size.Height <= 0)
                throw new ArgumentException("Size must have positive width and height.", nameof(size));

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