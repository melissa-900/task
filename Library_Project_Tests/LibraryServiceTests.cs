using Library_Project.Model;
using Library_Project.Services;
using Library_Project.Services.Interfaces;
using Moq;

namespace Library_Project_Tests
{
    public class LibraryServiceTests
    {
        private readonly Mock<IBookRepository> _repoMock;
        private readonly Mock<IMemberService> _memberMock;
        private readonly Mock<INotificationService> _notifMock;
        private readonly LibraryService _service;

        public LibraryServiceTests()
        {
            _repoMock = new Mock<IBookRepository>();
            _memberMock = new Mock<IMemberService>();
            _notifMock = new Mock<INotificationService>();

            _service = new LibraryService(_repoMock.Object, _memberMock.Object, _notifMock.Object);
        }

        private static Book CreateBook(string title, int copies)
        {
            return new Book { Title = title, Copies = copies };
        }

        /// <summary>
        /// Кидає ArgumentException, якщо назва книги порожня або null 
        /// ([Theory]/[InlineData], Assert.Throws)
        /// </summary>
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void AddBook_InvalidTitle_ThrowsArgumentException(string? title)
        {
            // Arrange
            int copies = 1;

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(title!, copies));
        }

        /// <summary>
        /// Кидає ArgumentException, якщо кількість примірників менше або дорівнює нулю 
        /// ([Theory]/[InlineData], Assert.Throws)
        /// </summary>
        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-5)]
        public void AddBook_NonPositiveCopies_ThrowsArgumentException(int copies)
        {
            // Arrange
            string title = "Book1";

            // Act & Assert
            Assert.Throws<ArgumentException>(() => _service.AddBook(title, copies));
        }

        /// <summary>
        /// Зберігає нову книгу з переданою назвою та кількістю примірників 
        /// (It.Is, Verify)
        /// </summary>
        [Fact]
        public void AddBook_NewBook_SavesWithCorrectData()
        {
            // Arrange
            string title = "Book1";
            int copies = 3;
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            _service.AddBook(title, copies);

            // Assert
            _repoMock.Verify(
                r => r.SaveBook(It.Is<Book>(b => b.Title == title && b.Copies == copies)),
                Times.Once);
        }

        /// <summary>
        /// Збільшує кількість примірників, якщо книга вже є в репозиторії 
        /// (Assert.Equal)
        /// </summary>
        [Fact]
        public void AddBook_ExistingBook_IncreasesCopies()
        {
            // Arrange
            string title = "Book1";
            var existingBook = CreateBook(title, 2);
            _repoMock.Setup(r => r.FindBook(title)).Returns(existingBook);

            // Act
            _service.AddBook(title, 3);

            // Assert
            Assert.Equal(5, existingBook.Copies);
        }

        /// <summary>
        /// Викликає SaveBook після оновлення кількості примірників існуючої книги 
        /// (Verify)
        /// </summary>
        [Fact]
        public void AddBook_ExistingBook_CallsSaveBook()
        {
            // Arrange
            string title = "Book1";
            var existingBook = CreateBook(title, 2);
            _repoMock.Setup(r => r.FindBook(title)).Returns(existingBook);

            // Act
            _service.AddBook(title, 1);

            // Assert
            _repoMock.Verify(r => r.SaveBook(existingBook));
        }

        /// <summary>
        /// Кидає InvalidOperationException, якщо читач невалідний 
        /// (Assert.Throws)
        /// </summary>
        [Fact]
        public void BorrowBook_InvalidMember_ThrowsInvalidOperationException()
        {
            // Arrange
            int memberId = 404;
            string title = "Book1";
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(false);

            // Act & Assert
            Assert.Throws<InvalidOperationException>(() => _service.BorrowBook(memberId, title));
        }

        /// <summary>
        /// Повертає false, якщо запитаної книги немає в репозиторії 
        /// (Assert.False)
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "This Book Does Not Exist";
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Не надсилає сповіщення про видачу, якщо книгу не знайдено 
        /// (Verify Times.Never, It.IsAny)
        /// </summary>
        [Fact]
        public void BorrowBook_BookNotFound_DoesNotNotify()
        {
            // Arrange
            int memberId = 1;
            string title = "This Book Does Not Exist";
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            _service.BorrowBook(memberId, title);

            // Assert
            _notifMock.Verify(n => n.NotifyBorrow(It.IsAny<int>(), It.IsAny<string>()), Times.Never);
        }

        /// <summary>
        /// Повертає false, якщо примірників книги не залишилося 
        /// (Assert.False)
        /// </summary>
        [Fact]
        public void BorrowBook_NoCopiesLeft_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "Rare Book";
            var book = CreateBook(title, 0);
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Повертає true при успішній видачі доступної книги валідному читачу 
        /// (Assert.True)
        /// </summary>
        [Fact]
        public void BorrowBook_AvailableBook_ReturnsTrue()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 2);
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            bool result = _service.BorrowBook(memberId, title);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Зменшує кількість примірників книги на 1 після видачі 
        /// (Assert.Equal)
        /// </summary>
        [Fact]
        public void BorrowBook_AvailableBook_DecreasesCopies()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 2);
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            _service.BorrowBook(memberId, title);

            // Assert
            Assert.Equal(1, book.Copies);
        }

        /// <summary>
        /// Надсилає сповіщення про видачу рівно один раз 
        /// (Verify Times.Exactly)
        /// </summary>
        [Fact]
        public void BorrowBook_AvailableBook_NotifiesBorrowExactlyOnce()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 1);
            _memberMock.Setup(m => m.IsValidMember(memberId)).Returns(true);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            _service.BorrowBook(memberId, title);

            // Assert
            _notifMock.Verify(n => n.NotifyBorrow(memberId, title), Times.Exactly(1));
        }

        /// <summary>
        /// Повертає false, якщо повернути книгу, якої немає в базі 
        /// (Assert.False)
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_ReturnsFalse()
        {
            // Arrange
            int memberId = 1;
            string title = "This Book Does Not Exist";
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.False(result);
        }

        /// <summary>
        /// Не викликає SaveBook, якщо повертається неіснуюча книга 
        /// (Verify Times.Never, It.IsAny)
        /// </summary>
        [Fact]
        public void ReturnBook_BookNotFound_NeverSavesBook()
        {
            // Arrange
            int memberId = 1;
            string title = "This Book Does Not Exist";
            _repoMock.Setup(r => r.FindBook(title)).Returns((Book)null!);

            // Act
            _service.ReturnBook(memberId, title);

            // Assert
            _repoMock.Verify(r => r.SaveBook(It.IsAny<Book>()), Times.Never);
        }

        /// <summary>
        /// Повертає true при успішному поверненні існуючої книги
        /// (Assert.True)
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_ReturnsTrue()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 1);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            bool result = _service.ReturnBook(memberId, title);

            // Assert
            Assert.True(result);
        }

        /// <summary>
        /// Збільшує кількість примірників на 1 при поверненні книги 
        /// (Assert.Equal)
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_IncreasesCopies()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 1);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            _service.ReturnBook(memberId, title);

            // Assert
            Assert.Equal(2, book.Copies);
        }

        /// <summary>
        /// Надсилає сповіщення про повернення рівно один раз 
        /// (Verify Times.Exactly)
        /// </summary>
        [Fact]
        public void ReturnBook_ExistingBook_NotifiesReturnExactlyOnce()
        {
            // Arrange
            int memberId = 1;
            string title = "Book1";
            var book = CreateBook(title, 1);
            _repoMock.Setup(r => r.FindBook(title)).Returns(book);

            // Act
            _service.ReturnBook(memberId, title);

            // Assert
            _notifMock.Verify(n => n.NotifyReturn(memberId, title), Times.Exactly(1));
        }

        /// <summary>
        /// Включає у результат книгу, яка є в наявності 
        /// (Assert.Contains)
        /// </summary>
        [Fact]
        public void GetAvailableBooks_MixedStock_ContainsAvailableBook()
        {
            // Arrange
            var availableBook = CreateBook("Book1", 2);
            var rareBook = CreateBook("Rare Book", 0);
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book> { availableBook, rareBook });

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Contains(availableBook, result);
        }

        /// <summary>
        /// Повертає непорожній список, якщо є книги з додатною кількістю примірників 
        /// (Assert.NotEmpty)
        /// </summary>
        [Fact]
        public void GetAvailableBooks_HasBooksInStock_ReturnsNotEmpty()
        {
            // Arrange
            var books = new List<Book> { CreateBook("Book1", 3) };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotEmpty(result);
        }

        /// <summary>
        /// Повертає порожній список, якщо у всіх книг 0 примірників 
        /// (Assert.Empty)
        /// </summary>
        [Fact]
        public void GetAvailableBooks_AllOutOfStock_ReturnsEmpty()
        {
            // Arrange
            var books = new List<Book> { CreateBook("Rare Book", 0) };
            _repoMock.Setup(r => r.GetAllBooks()).Returns(books);

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.Empty(result);
        }

        /// <summary>
        /// Повертає не-null список навіть при порожньому репозиторії 
        /// (Assert.NotNull)
        /// </summary>
        [Fact]
        public void GetAvailableBooks_EmptyRepository_ReturnsNotNull()
        {
            // Arrange
            _repoMock.Setup(r => r.GetAllBooks()).Returns(new List<Book>());

            // Act
            var result = _service.GetAvailableBooks();

            // Assert
            Assert.NotNull(result);
        }
    }
}