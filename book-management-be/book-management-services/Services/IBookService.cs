﻿using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using book_management_models;
using book_management_models.DTOs.BookDTOs;

namespace book_management_services.Services
{
    public interface IBookService
    {
        IEnumerable<Book> GetListBooks();
        IEnumerable<Book> GetBooksByCategory(string categoryName);
        Book GetBookById(Guid id);
        Task<Guid> AddNewBook(BookForCreateDTO newBook);
        Task<bool> UpdateBook(BookForUpdateDto bookForUpdate, Guid bookId);
        Task<bool> DeleteBook(Guid bookId);
        IEnumerable<Book> GetAllPaging(out int totalRow, int searchKey, string searchTitle, int page, int pageSize);
        BookForDetailDTO GetDetailBookData(Guid bookId);

        IEnumerable<Book> GetAllBookByFilter(string searchTitle);
    }
}