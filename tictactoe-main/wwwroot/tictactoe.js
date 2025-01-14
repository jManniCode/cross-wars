let player = null;
let game = null;
let playedTiles = [];
let tiles = [];
let selectedTile = null;

document.addEventListener("DOMContentLoaded", () => {
  const form = document.getElementById("tictactoe");

  if (form) {
    // Skapa och lägg till 100 input-element
    for (let i = 0; i < 100; i++) {
      const input = document.createElement("input");
      input.type = "text";
      input.id = `input-${i}`;
      input.name = `input-${i}`;
      input.disabled = true; // Bör vara inaktiverad från början
      input.addEventListener("click", () => selectTile(input)); // Lägg till click-händelse
      form.appendChild(input);
      tiles.push(i);
    }
  } else {
    console.error("Div-elementet med id 'tictactoe' hittades inte.");
  }

  // Koppla knappen till funktionen för att skicka drag
  const submitButton = document.getElementById("submit-move");
  submitButton.addEventListener("click", submitMove);
});

function selectTile(tile) {
  if (!tile.disabled) {
    selectedTile = tile; // Spara vald ruta
    $('#message2').text(`Selected tile: ${tile.id}`);
  }
}
async function submitMove() {
  if (!selectedTile) {
    $('#message').text('Please select a tile before submitting.');
    return;
  }

  const tileIndex = tiles.indexOf(parseInt(selectedTile.id.replace('input-', '')));
  const inputValue = selectedTile.value.toUpperCase();

  // Validera inmatningen
  if (!/^[A-Z]$/.test(inputValue)) {
    $('#message').text('Please enter a single letter (A-Z).');
    selectedTile.value = ''; // Rensa ogiltig inmatning
    return;
  }

  // Skicka bokstaven till servern
  try {
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
      selectedTile.value = inputValue; // Visa bokstaven
      selectedTile.disabled = true; // Lås rutan
      selectedTile = null; // Återställ vald ruta
      $('#message').text('Move accepted!');
      refresh(); // Uppdatera spelytan
    } else {
      $('#message').text('This tile is already taken.');
      selectedTile.value = ''; // Rensa rutan om draget inte accepterades
    }
  } catch (error) {
    console.error('Error during fetch:', error);
    $('#message').text('An error occurred while sending your move.');
  }
}
// refresh is a sequence to handle reflecting the current game state for any connected user
async function refresh() {
  try {
    // Kontrollera om spelare och spel är konfigurerade
    if (!player) {
      $('#add-player').show();
    } else {
      $('#add-player').hide();

      if (!game) {
        $('#add-game').show();
      } else {
        $('#add-game').hide();
      }
    }

    // Om spelare eller spel saknas, inaktivera spelplanen
    if (!player || !game) {
      $('#tictactoe input').prop('disabled', true);
    } else {
      // Hämta de senaste spelade brickorna
      const response = await fetch('/api/played-tiles/' + game.id);
      const playedTilesUpdate = await response.json();

      // Endast uppdatera om något har ändrats
      if (JSON.stringify(playedTilesUpdate) !== JSON.stringify(playedTiles)) {
        playedTiles = playedTilesUpdate;
        showPlayableTiles(playedTiles);
        tellTurn(playedTiles);
      }
    }
  } catch (error) {
    console.error('Error in refresh:', error);
  } finally {
    // Kör `refresh` igen efter en liten fördröjning
    setTimeout(refresh, 1000); // Uppdatera varje sekund
  }
}
// first call refresh when page has loaded to reflect inital state / rebuild current state
refresh()


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

async function checkWin() {
  const response = await fetch('/api/check-win/' + game.id);
  const win = await response.json();
  if(win){
    $('#message').text('Raden ' + win.join(' - ') + ' vann!')
    // disable tiles
    $('#tictactoe input').prop('disabled', true);
    // show winning row
    $('#tictactoe input').each(function() {
      if(win.includes($(this).index())){
        $(this).css('background-color', 'yellow')
      }
    })
    return true;
  }
  return false;
}


$('#add-player').on('submit', addPlayer) // onsubmit for the addPlayer form

async function addPlayer(e) {
  e.preventDefault()
  const playerName = $('#add-player>[name="name"]').val()
  const response = await fetch('/api/add-player/', { // post
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ name: playerName })
  });
  player = await response.json();
  $('#message').text(player.name + ' was added to the game')
  refresh()
}

$('#add-game').on('submit', addGame) // onsubmit for the addGame form

async function addGame(e) {
  e.preventDefault()
  const gamecode = $('#add-game>[name="gamecode"]').val()
  const response = await fetch('/api/current-game/' + gamecode)
  game = await response.json();
  player.tile = (player.id === game.player_1)?'X':'O'; // Are you player 1? Then you get X, else O.
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

  $(this).off('keydown').on('keydown', async function (e) {
    console.log('Key pressed:', e.key); // Debug-logg
    if (e.key === 'Enter') {
      // Validera inmatningen
      if (!/^[A-Z]$/.test(inputValue)) {
        $('#message').text('Please enter a single letter (A-Z).');
        $(this).val(''); // Rensa ogiltig inmatning
        return;
      }

      // Skicka bokstaven till servern
      const response = await fetch('/api/play-tile/', {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({
          tile: tileIndex,
          player: player.id,
          game: game.id,
          value: inputValue // Lagra bokstaven
        })
      });

      const moveAccepted = await response.json();

      if (moveAccepted) {
        $(this).val(inputValue).prop('disabled', true); // Lås rutan och visa bokstaven
        $('#message').text(`Move accepted with '${inputValue}' at index ${tileIndex}`);
        refresh(); // Uppdatera spelet efter draget
      } else {
        $('#message').text('This tile is already taken.');
        $(this).val(''); // Rensa rutan om draget inte accepterades
      }
    }
  });
}

$('#tictactoe>input').on('click', playTile);
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
