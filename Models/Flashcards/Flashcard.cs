using System.Collections.ObjectModel;

namespace StudyHelper.Models.Flashcards
{
    /// <summary>
    /// Represents a single flashcard with front and back content.
    /// </summary>
    public class Flashcard
    {
        
        // Gets or sets the content for the front side of the flashcard.
        public string Front { get; set; }

        // Gets or sets the content for the back side of the flashcard.
        
        public string Back { get; set; }
    }


    /// <summary>
    /// Represents a collection of flashcards as a deck.
    /// </summary>
    public class FlashcardDeck
    {
        // Gets or sets the name of the flashcard deck.
        public string Name { get; set; }


        // Gets the observable collection of flashcards within the deck.
        public ObservableCollection<Flashcard> Cards { get; set; } = new ObservableCollection<Flashcard>();

        /// <summary>
        /// Returns a string representation of the deck, showing its name and card count to display.
        /// </summary>
        /// <returns>A formatted string with the deck name and card count.</returns>
        public override string ToString()
        {
            return $"{Name} ({Cards.Count} cards)";
        }
    }
}