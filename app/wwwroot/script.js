const crossword = document.getElementById("crossword");

// Loop to create 100 grid items (10x10 grid)
const gridlist=[]; 
for(let j=0; j<10 ; j++){
    gridlist.push([]); 
}
for (let i = 0; i < 100; i++) {
    // Create a new div element for each grid item
    const gridItem = document.createElement("div");
    gridlist[ Math.floor(i/10)].push(gridItem); 
    gridItem.classList.add("grid-item");
    
    gridItem.classList.add("inactive")
    gridItem.textContent = i; // Add a number to each cell
    crossword.appendChild(gridItem); // Add to the grid container
}


const tile= document.getElementby







