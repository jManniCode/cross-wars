CREATE DATABASE tictactoe;

create table players
(
    id       serial
        constraint players_pk
            primary key,
    name     text not null,
    clientid text not null
);

alter table players
    owner to postgres;

create unique index players_name_uindex
    on players (name);

create table games
(
    id       serial
        constraint games_pk
            primary key,
    player_1 integer not null
        constraint games_players_id_fk
            references players,
    player_2 integer not null
        constraint games_players_id_fk2
            references players,
    gamecode text
);

alter table games
    owner to postgres;

create table moves
(
    tile   integer not null,
    player integer not null
        constraint moves_players_id_fk
            references players,
    game   integer not null
        constraint moves_games_id_fk
            references games
);

alter table moves
    owner to postgres;

create index moves_tile_game_index
    on moves (tile, game);

