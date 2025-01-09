const crossword = document.getElementById("grid-container");

// Loop to create 100 grid items (10x10 grid)
for (let i = 1; i <= 100; i++) {
    // Create a new div element for each grid item
    const gridItem = document.createElement("div");
    gridItem.classList.add("grid-item");
    gridItem.textContent = i; // Add a number to each cell
    gridContainer.appendChild(gridItem); // Add to the grid container
}