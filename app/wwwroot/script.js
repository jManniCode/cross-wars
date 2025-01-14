const crossword = document.getElementById("crossword");

// Loop to create 100 grid items (10x10 grid)
const gridlist = [];
for (let j = 0; j < 10; j++) {
  gridlist.push([]);
}
for (let i = 0; i < 100; i++) {
  // Create a new div element for each grid item
  const gridItem = document.createElement("div");
  gridlist[Math.floor(i / 10)].push(gridItem);
  gridItem.classList.add("grid-item");

  gridItem.classList.add("inactive");
  gridItem.textContent = i; // Add a number to each cell
  crossword.appendChild(gridItem); // Add to the grid container
}

const tile = document.getElementby;

function getIndex(row, column, columnlength) {
  return (id = row * columnlength + column);
}

function getCordinate(index, columnlength) {
  let row = Math.floor(index / columnlength);
  let column = index % columnlength;
  return [row, column];
}

const button = document.getElementById("addName");
const output = document.getElementById("output");

// Define a function to handle the button click
function handleButtonClick(e) {
  output.textContent = "Button was clicked!";
  saveWord(e);
}
async function saveWord(e) {
  e.preventDefault(); // Prevent page reload on form submit

  const fieldText = document.getElementById("nameField");
  const newWord = fieldText.value.trim(); // Trim whitespace
  if (!newWord) {
    console.error("Input field is empty.");
    output.textContent = "Please enter a word.";
    return;
  }

  console.log("newWord:", newWord);

  try {
    const response = await fetch("/new-player/", {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ word: newWord }),
    });

    if (!response.ok) {
      console.error("HTTP error:", response.status);
      output.textContent = `Error: ${response.status}`;
      return;
    }

    const data = await response.json();
    console.log("data:", data);
    output.textContent = data.message || "Player added successfully!";
  } catch (error) {
    console.error("Error:", error);
    output.textContent = "An error occurred while adding the player.";
  }
}


async function addGame(e) {
  e.preventDefault();

  const gamecode = document.getElementById("sessionField").value;


  try {
    const response = await fetch('/session/', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ word: gamecode }),
    });

    if (!response.ok) {
      console.error("HTTP error:", response.status);
      output.textContent = `Error: ${response.status}`;
      return;
    }

    const data = await response.json();
    console.log("data:", data);
    output.textContent = data.message || "Game added successfully!";
  } catch (error) {
    console.error("Error:", error);
    output.textContent = "An error occurred while adding the game.";
  }
}



// Attach the click event to the button
button.addEventListener("click", handleButtonClick);

const gameButton = document.getElementById("addGame");
gameButton.addEventListener("click", addGame);
