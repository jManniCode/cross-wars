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
function handleButtonClick() {
  output.textContent = "Button was clicked!";
  saveWord();
}
async function saveWord(e) {
  e.preventDefault(); // not reload page on form submit
  const fieldText = document.getElementById("nameField");
  const newWord = fieldText.val();
  console.log("newWord", newWord);
  const response = await fetch("/add-name/", {
    // post (save new)
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ word: newWord }),
  });
  console.log("response", response);
  const data = await response.json();
  console.log("data", data);
  output.textContent = newWord.text;
}

// Attach the click event to the button
button.addEventListener("click", handleButtonClick);
