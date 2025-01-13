const button = document.getElementById("addName");
const output = document.getElementById("output");

// Define a function to handle the button click
function handleButtonClick(e) {
  output.textContent = "Button was clicked!";
  saveWord(e);
}
async function saveWord(e) {
  e.preventDefault(); // Prevent page reload on form submit

  const fieldText = document.getElementById("player-name");
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

// Attach the click event to the button
button.addEventListener("click", handleButtonClick);
