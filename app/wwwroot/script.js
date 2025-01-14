const crossword = document.getElementById("crossword");

// Loop to create 100 grid items (10x10 grid)
const gridlist = [];
 loadCrossWord(); 
 // createEmptyTiles("2","1"); 
 compareLetter(1 , 4, 1, "c");






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
      body: JSON.stringify({ word: newWord })
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


async function createEmptyTiles(gameId,crosswordId){
  const response = await fetch("api/SetupEmptyTiles", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ Game:gameId, Crossword:crosswordId}),
  });
} 

// Attach the click event to the button
button.addEventListener("click", handleButtonClick);



async function loadCrossWord(){
  const crossWordInfo=  await getCrossWordInfo();
  let rowLength=parseInt(crossWordInfo[1]); 
  let columnLength=parseInt(crossWordInfo[2]);
  
  console.log(rowLength*columnLength); 
  
  crossword.innerHTML=""; // reset crossWord
  
  for (let i = 0; i <100 ; i++) {
    // Create a new div element for each grid item
    const gridItem = document.createElement("div");
    gridlist.push(gridItem);
    gridItem.classList.add("grid-item");

    gridItem.classList.add("inactive");
    gridItem.textContent = i; // Add a number to each cell
    crossword.appendChild(gridItem); // Add to the grid container
  }


}
async function GetValidTiles(crossword){
  let index= await fetch()   
}
async function getCrossWordInfo(){
  let index = await fetch("api/randomCrossWordInfo/ ");
  const infoString = await index.text();
  const info = infoString.split(',').filter(word => word.trim() !== ""); // Remove empty lines
  return info; 
}; 


async function compareLetter(crosswordId, row, column, char){
let response = await fetch("api/compareLetter", {
    method: "POST",
    headers: { "Content-Type": "application/json" },
    body: JSON.stringify({ crosswordId:crosswordId
      , row:row, column:column, letter:char }),
  });
}

async function getHints(crosswordId){
  let response = await fetch("api/getHints", {
    
  });
}