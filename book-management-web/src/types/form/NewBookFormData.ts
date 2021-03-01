type NewBookFormData = {
    title: string,
    pages: number,
    description: string,
    sku: string,
    authorName: string,
    publisherName: string,
    price: number,
    photos: File[]
}

export default NewBookFormData