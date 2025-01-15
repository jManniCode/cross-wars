let player = null;
let game = null;
let playedTiles = [];
let tiles = [];
let selectedTile = null;
let crosswordPlacements = {}; // Store placements for later validation


function CheckForWin(playedTiles, crosswordPlacements) {
  let cwPlCount = 0;
  Object.entries(crosswordPlacements).forEach(([tile, letter], index) => {
    cwPlCount+=1;
    //console.log(`Index: ${index}, Tile: ${tile}, Letter: ${letter}`);
    
  });
  console.log(`Played Tiles: ${playedTiles.length}, cwPlacements: ${cwPlCount}`);
  const p1Score = parseInt(document.getElementById("player1-score").innerText, 10);
  const p2Score = parseInt(document.getElementById("player2-score").innerText,10);
  
  console.log(`P1 score: ${p1Score}, P2 Score: ${p2Score}`)
  
  if(playedTiles.length === cwPlCount){
    
    $('#message').text('It is you turn, ' + player.name + ' to play a ' + player.tile)


  }
}


document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("tictactoe");
  
  if (form) {
    // Skapa och lägg till 100 input-element
    for (let i = 0; i < 100; i++) {
      const input = document.createElement("input");
      input.type = "text";
      input.id = `input-${i}`;
      input.name = `input-${i}`;
      input.disabled = true;
      input.addEventListener("click", () => selectTile(input)); // Lägg till click-händelse
      form.appendChild(input);
      tiles.push(i);
    }
  } else {
    console.error("Div-elementet med id 'tictactoe' hittades inte.");
  }
  
  const submitButton = document.getElementById("submit-move");
  submitButton.addEventListener("click", submitMove);
  $('#tictactoe>input').on('click', playTile);
  $('#add-player').on('submit', addPlayer)
  $('#add-game').on('submit', addGame)
  fetchCrossWordPlacements();
});

async function fetchCrossWordPlacements() {
  try {
    const response = await fetch('/api/cross-word-placements');
    if (!response.ok) {
      throw new Error(`HTTP error! status: ${response.status}`);
    }

    const placements = await response.json();

    placements.forEach(placement => {
      const tile = getIndex(parseInt(placement.row), parseInt(placement.column), 10);
      crosswordPlacements[tile] = placement.letter.toUpperCase();
    });

    for (let i = 0; i < 100; i++) {
      const inputElement = document.getElementById(`input-${i}`);
      if (inputElement) {
        if (crosswordPlacements[i]) {
          inputElement.disabled = false; // Enable user input
        } else {
          console.log("ICKE AKTIV TILES")
          inputElement.style.backgroundColor = 'transparent';
          inputElement.style.border = 'none';
          inputElement.readOnly = true;
          inputElement.disabled = true;
          inputElement.style.pointerEvents = 'none';
          inputElement.style.cursor = 'default';
          
        }
      }
    }
     
  } catch (error) {
    console.error('Error fetching crossword placements:', error);
  }
  
}

function getIndex(row, column, columnlength) {
  let id = row * columnlength + column
  return (id);
}

function selectTile(tile) {
  if (!tile.disabled) {
    selectedTile = tile; // Spara vald ruta
  }
}
function updateTileColors(playedTilesWithStatus) {
  playedTilesWithStatus.forEach(tile => {
    const inputElement = document.getElementById(`input-${tile.tile}`);
    if (inputElement) {
      inputElement.value = tile.value; // Sätt bokstaven
      inputElement.disabled = true; // Lås rutan
      inputElement.style.backgroundColor =
          tile.status === "correct" ? "lightgreen" : "lightcoral"; // Sätt färg
    }
  });
}

function tellTurn(playedTiles){
  let yourMoves = 0;
  let otherMoves = 0;
  for(let tile of playedTiles){
    if(tile.player === player.id){
      yourMoves++
    }else{
      otherMoves++
    }
  }
  // player with tile X plays first
  if(player.tile === 'X' && yourMoves <= otherMoves || player.tile === 'O' && yourMoves < otherMoves){
    $('#message').text('It is you turn, ' + player.name + ' to play a ' + player.tile)
    $('#tictactoe input').prop('disabled', false);
  }else{
    $('#message').text('It is their turn')
    // if it's their turn we disable all tiles for us
    $('#tictactoe input').prop('disabled', true);
  }
}

async function refresh() {
  try {
    if (!player) {
      $('#add-player').show();
      $('#add-game').hide();
      $('#tictactoe input').prop('disabled', true);
      return;
    } else {
      $('#add-player').hide();
    }

    if (!game) {
      $('#add-game').show();
      $('#tictactoe input').prop('disabled', true);
      return;
    } else {
      $('#add-game').hide();
    }
    
    const response = await fetch(`/api/played-tiles-status/${game.id}`);
    const playedTilesWithStatus = await response.json();
    
    updateTileColors(playedTilesWithStatus);
    
    const tilesResponse = await fetch(`/api/played-tiles/${game.id}`);
    const playedTilesUpdate = await tilesResponse.json();
    
    if (JSON.stringify(playedTilesUpdate) !== JSON.stringify(playedTiles)) {
      playedTiles = playedTilesUpdate;
      showPlayableTiles(playedTiles);
    }
    
    tellTurn(playedTiles);
    await updateScores();
  } catch (error) {
    console.error('Error in refresh:', error);
  } finally {
    setTimeout(refresh, 1000);
  }
}
refresh()

async function submitMove() {
  if (!selectedTile) {
    $('#message').text('Please select a tile before submitting.');
    return;
  }
  CheckForWin(playedTiles,crosswordPlacements)
  const tileIndex = tiles.indexOf(parseInt(selectedTile.id.replace('input-', '')));
  const inputValue = selectedTile.value.toUpperCase();

  if (!/^[A-Z]$/.test(inputValue)) {
    $('#message').text('Please enter a single letter (A-Z).');
    selectedTile.value = ''; // Rensa ogiltig inmatning
    return;
  }

  try {
    // Validera draget via backend
    const validateResponse = await fetch('/api/validate-move', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tile: tileIndex,
        value: inputValue,
        game: game.id
      })
    });

    const isValid = await validateResponse.json();

    // Spara draget i databasen oavsett korrekthet
    const saveResponse = await fetch('/api/play-tile/', {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        tile: tileIndex,
        player: player.id,
        game: game.id,
        value: inputValue
      })
    });

    const moveAccepted = await saveResponse.json();

    if (moveAccepted) {
      const playedTilesWithStatusResponse = await fetch(`/api/played-tiles-status/${game.id}`);
      const playedTilesWithStatus = await playedTilesWithStatusResponse.json();
      updateTileColors(playedTilesWithStatus);

      selectedTile.value = inputValue;
      selectedTile.disabled = true;
      selectedTile = null;
      refresh();
    } else {
      $('#message').text('This tile is already taken.');
      selectedTile.value = '';
    }
  } catch (error) {
    console.error('Error during validation or move submission:', error);
    $('#message').text('An error occurred while validating your move.');
  }
}
async function updateScores() {
  const response = await fetch(`/api/game-scores/${game.id}`);
  const data = await response.json();

  // Kontrollera att dessa element faktiskt finns i HTML
  document.getElementById("player1-score").innerText = data.player1Score;
  document.getElementById("player2-score").innerText = data.player2Score;
}

async function addPlayer(e) {
  e.preventDefault()
  const playerName = $('#add-player>[name="name"]').val()
  const response = await fetch('/api/add-player/', {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: playerName })
  });
  player = await response.json();
  $('#message').text(player.name + ' was added to the game')
  refresh()
}

async function addGame(e) {
  e.preventDefault()
  const gamecode = $('#add-game>[name="gamecode"]').val()
  const response = await fetch('/api/current-game/' + gamecode)
  game = await response.json();
  player.tile = (player.id === game.player_1)?'X':'O';
  if(game){
    $('#message').text('Connected to ' + game.gamecode)
  }else{
    $('#message').text('Found no game with the code ' + gamecode)
  }
  refresh()
}
async function playTile() {
  let tileIndex = $(this).index();
  // Tillåt inmatning i rutan
  $(this).prop('disabled', false).focus();
  
      // Skicka bokstaven till servern
      const response = await fetch('/api/play-tile/', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          tile: tileIndex,
          player: player.id,
          game: game.id,
          value: inputValue
        })
      });

      const moveAccepted = await response.json();
      if (moveAccepted) {
        $(this).val(inputValue).prop('disabled', true); // Lås rutan och visa bokstaven
        $('#message').text(`Move accepted with '${inputValue}' at index ${tileIndex}`);
        refresh();
      } else {
        $('#message').text('This tile is already taken.');
        $(this).val('');
      }
}

function showPlayableTiles(playedTiles) {
  const playedTilesHash = {};
  for (let tile of playedTiles) {
    playedTilesHash[tile.tile] = tile; // Skapa en hash för snabb åtkomst till spelade rutor
  }

  $('#tictactoe input').each(function (index) {
    const tile = playedTilesHash[index]; // Hämta information om rutan från hashen

    if (tile) {
      // Om rutan redan är spelad, visa bokstaven och lås rutan
      $(this).val(tile.value).prop('disabled', true);
    } else {
      // Om det är spelarens tur, gör rutan spelbar
      if (player.id === game.currentTurn) {
        $(this).prop('disabled', false).off('click').on('click', playTile);
      } else {
        // Annars inaktivera rutan
        $(this).prop('disabled', true);
      }
    }
  });
}
