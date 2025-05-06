using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RideMatchProject.LoginClasses
{
    /// <summary>
    /// Represents the result of a validation operation, indicating whether the validation was successful
    /// and providing an optional error message if it failed.
    /// </summary>
    public class ValidationResult
    {
        /// <summary>
        /// Gets a value indicating whether the validation was successful.
        /// </summary>
        /// <value>
        /// <c>true</c> if the validation passed; otherwise, <c>false</c>.
        /// </value>
        public bool IsValid { get; }

        /// <summary>
        /// Gets the error message associated with the validation failure, if any.
        /// </summary>
        /// <value>
        /// A string containing the error message if <see cref="IsValid"/> is <c>false</c>;
        /// otherwise, an empty string or a message indicating no error.
        /// </value>
        public string ErrorMessage { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="ValidationResult"/> class.
        /// </summary>
        /// <param name="isValid">A boolean indicating whether the validation was successful.</param>
        /// <param name="errorMessage">The error message to associate with the validation result. 
        /// Can be null or empty if the validation is successful.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="errorMessage"/> is null and <paramref name="isValid"/> is <c>false</c>.</exception>
        public ValidationResult(bool isValid, string errorMessage)
        {
            if (!isValid && errorMessage == null)
                throw new ArgumentNullException(nameof(errorMessage), "Error message cannot be null when validation fails.");

            IsValid = isValid;
            ErrorMessage = errorMessage ?? string.Empty;
        }
    }
}