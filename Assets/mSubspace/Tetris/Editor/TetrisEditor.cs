using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace mSubspace.Games
{
    public class TetrisEditor : EditorWindow
    {
        class TetrisPiece
        {
            public enum PieceType
            {
                T = 0,
                Straight = 1,
                L = 2,
                LReverse = 3,
                Square = 4,
                Squiggle = 5,
                SquiggleReverse = 6
            }

            public readonly bool[,] PIECE_MAPS = new bool[,]
            {
                // T Piece
                {
                    false, false, false, false,
                    true,   true,  true, false,
                    false,  true, false, false,
                    false, false, false, false
                },
                // Straight Piece
                {
                    false,  true, false, false,
                    false,  true, false, false,
                    false,  true, false, false,
                    false,  true, false, false
                },
                // L Piece
                {
                    false, false, false, false,
                    false,  true, false, false,
                    false,  true, false, false,
                    false,  true,  true, false
                },
                // L Piece (Reverse)
                {
                    false, false, false, false,
                    false,  false, true, false,
                    false,  false, true, false,
                    false,  true,  true, false
                },
                // Square Piece
                {
                    false, false, false, false,
                    false,  true,  true, false,
                    false,  true,  true, false,
                    false, false, false, false
                },
                // Squiggle Piece
                {
                    false, false, false, false,
                    false,  true,  true, false,
                     true,  true, false, false,
                    false, false, false, false
                },
                // Squiggle Piece (Reverse)
                {
                    false, false, false, false,
                     true,  true, false, false,
                    false,  true,  true, false,
                    false, false, false, false
                }
            };

            // Clockwise
            private readonly int[] ROTATION_MAPPING = new int[]
            {
                12,  8, 4, 0,
                13,  9, 5, 1,
                14, 10, 6, 2,
                15, 11, 7, 3
            };

            // Counter-clockwise
            private readonly int[] REVERSE_ROTATION_MAPPING = new int[]
            {
                3, 7, 11, 15,
                2, 6, 10, 14,
                1, 5,  9, 13,
                0, 4,  8, 12
            };

            // Standardised colors according to Wikipedia.
            // Also apparently all the pieces have letters for names but whatever.
            private readonly Color[] PIECE_COLORS = new Color[]
            {
                new Color(0.5f, 0, 1, 1), // T
                Color.cyan, // Straight
                new Color(1, 0.5f, 0, 1), // L
                Color.blue, // L (Reverse)
                Color.yellow, // Square
                Color.green, // Squiggle
                Color.red // Squiggle (Reverse)
            };

            public int col;
            public int row;

            public bool[] pieceData;
            public Color color;
            public PieceType pieceType;

            public TetrisPiece()
            {
                // Construct with a random piece.
                int randPiece = (int)(Random.value * PIECE_MAPS.GetLength(0));
                pieceType = (PieceType)randPiece;
                pieceData = new bool[16];
                for (int row = 0; row < 4; ++row)
                {
                    for (int col = 0; col < 4; ++col)
                    {
                        int idx = Index(col, row, 4);
                        pieceData[idx] = PIECE_MAPS[randPiece, idx];
                    }
                }
                color = PIECE_COLORS[randPiece];
            }

            public void RotatePiece()
            {
                bool[] rotatedPiece = new bool[16];
                for (int i = 0; i < 16; ++i)
                {
                    rotatedPiece[i] = pieceData[ROTATION_MAPPING[i]];
                }
                pieceData = rotatedPiece;
            }

            public void RotatePieceReverse()
            {
                bool[] rotatedPiece = new bool[16];
                for (int i = 0; i < 16; ++i)
                {
                    rotatedPiece[i] = pieceData[REVERSE_ROTATION_MAPPING[i]];
                }
                pieceData = rotatedPiece;
            }

            public bool IsHit(int col, int row)
            {
                int colDist = col - this.col;
                int rowDist = row - this.row;

                if (3 < colDist || 3 < rowDist || 0 > colDist || 0 > rowDist) return false;

                return pieceData[Index(colDist, rowDist, 4)];
            }
        }

        class GameBoard
        {
            class BoardTile
            {
                public bool isFilled;
                public Color color;
            }

            public struct InfoMessage
            {
                public string message;
                public Color color;
                public double messageTime;

                /// <summary>Negative values mean infinite duration.</summary>
                public double messageDuration;

                public bool IsNotExpired()
                {
                    return ( 0 > messageDuration ) || ( ( EditorApplication.timeSinceStartup - messageTime ) <= messageDuration );
                }
            }

            public enum ValidityResult
            {
                Valid,
                OutOfBounds,
                IntersectsBlock
            }

            // Layout Data
            public const int BLOCK_PIXEL_SIZE = 14; // Around the size of a toggle button.
            public const int GHOST_ROW_COUNT = 2;

            Rect gameBoardRect;

            int gameBoardRows = 0;
            int gameBoardCols = 0;

            int gameBoardWidthPx;
            int gameBoardHeightPx;

            float xLeftOffset;
            float yTopOffset;

            // Board Data
            BoardTile[] gameBoard = new BoardTile[0];
            TetrisPiece currentPiece = null;
            public InfoMessage infoMessage = new InfoMessage { message = "", color = Color.white };

            // Player Data
            int totalRowsCleared = 0;
            int tSpinCount = 0;

            // Move Data
            readonly float FALL_SPEED = 2f; // blocks per second.
            readonly int PIECE_DOWN_ATTEMPTS_BEFORE_LOCKING = 2;

            double lastCurrentPieceMoveDownTime = 0f;
            int failedCurrentPieceDownAttempts = 0;
            bool isPlaying = true;

            public GameBoard(Rect gameBoardRect)
            {
                UpdateBoardLayout(gameBoardRect);
                SpawnPiece();
            }

            public void Draw(Rect gameBoardRect)
            {
                UpdateBoardLayout(gameBoardRect);

                for (int row = GHOST_ROW_COUNT; row < gameBoardRows; ++row)
                {
                    for (int col = 0; col < gameBoardCols; ++col)
                    {
                        // The board tile data should be starting after the ghost rows,
                        // but the Rect should start at row = 0 so subtract back the ghost row count
                        // for the Rect calculation only.
                        Rect checkboxRect = new Rect(
                            xLeftOffset +  col * BLOCK_PIXEL_SIZE,
                            yTopOffset  + (row - GHOST_ROW_COUNT) * BLOCK_PIXEL_SIZE,
                            BLOCK_PIXEL_SIZE,
                            BLOCK_PIXEL_SIZE
                          );

                        BoardTile tile = gameBoard[Index(col, row, gameBoardCols)];
                        bool currentPieceHit = null == currentPiece ? false : currentPiece.IsHit(col, row);

                        if (tile.isFilled)
                        {
                            GUI.color = tile.color;
                        }
                        else if (currentPieceHit)
                        {
                            GUI.color = currentPiece.color;
                        }

                        //EditorGUI.LabelField(blockRect, "", EditorStyles.helpBox);
                        //GUI.color = Color.white;

                        bool tileState = tile.isFilled || currentPieceHit;
                        bool newTileState = EditorGUI.Toggle(checkboxRect, tileState);
                        if (newTileState != tileState)
                        {
                            SetInfoMessage("Cheater! >_<", new Color(1, 0.5f, 1, 1), 10);
                            tile.isFilled = newTileState;
                            tile.color = Color.magenta;
                            ClearFullRows();
                        }
                        GUI.color = Color.white;
                    }
                }
            }

            private void SetInfoMessage(string message, Color color, double duration = -1)
            {
                infoMessage = new InfoMessage { message = message, color = color, messageTime = EditorApplication.timeSinceStartup, messageDuration = duration };
            }

            public InfoMessage GetInfoMessage()
            {
                return infoMessage;
            }

            private void UpdateBoardLayout(Rect gameBoardRect)
            {
                this.gameBoardRect = gameBoardRect;

                float fullAreaWidth = gameBoardRect.width;
                float fullAreaHeight = gameBoardRect.height;

                int newGameBoardCols = (int)(fullAreaWidth / BLOCK_PIXEL_SIZE);
                int newGameBoardRows = (int)(fullAreaHeight / BLOCK_PIXEL_SIZE) + GHOST_ROW_COUNT;

                if (newGameBoardRows != gameBoardRows || newGameBoardCols != gameBoardCols)
                {
                    gameBoardWidthPx = newGameBoardCols * BLOCK_PIXEL_SIZE;
                    gameBoardHeightPx = (newGameBoardRows - GHOST_ROW_COUNT) * BLOCK_PIXEL_SIZE;

                    xLeftOffset = gameBoardRect.x + (fullAreaWidth - gameBoardWidthPx) / 2.0f;
                    yTopOffset = gameBoardRect.y + (fullAreaHeight - gameBoardHeightPx) / 2.0f;

                    BoardTile[] newGameBoard = new BoardTile[newGameBoardCols * newGameBoardRows];
                    
                    Copy(ref gameBoard, gameBoardCols, gameBoardRows, ref newGameBoard, newGameBoardCols, newGameBoardRows);

                    // Only copying the current board contents to the new layout mostly works, but it can leave islands of blocks that
                    // can't easily be detected by just shifting everything down to the lowest spot (mostly if you resize more than once).
                    // can't easily be detected by just shifting everything down to the lowest spot (mostly if you resize more than once).
                    // Maybe revisit this later, but for now let's clear the board after the resize.
                    ClearBoard(ref newGameBoard, newGameBoardCols, newGameBoardRows);
                    ResetState();

                    gameBoard = newGameBoard;

                    gameBoardCols = newGameBoardCols;
                    gameBoardRows = newGameBoardRows;
                }
            }

            private void ClearBoard(ref BoardTile[] board, int cols, int rows)
            {
                for (int row = 0; row < rows; ++row)
                {
                    for (int col = 0; col < cols; ++col)
                    {
                        BoardTile tile = board[Index(col, row, cols)];
                        tile.isFilled = false;
                    }
                }
            }

            private void ResetState()
            {
                totalRowsCleared = 0;
                tSpinCount = 0;
                failedCurrentPieceDownAttempts = 0;
                SetInfoMessage("Game Reset", Color.white, 1);
                isPlaying = true;
            }

            public void Reset()
            {
                ClearBoard(ref gameBoard, gameBoardCols, gameBoardRows);
                ResetState();
                SpawnPiece();
            }

            private void Copy(ref BoardTile[] oldBoard, int oldCols, int oldRows, ref BoardTile[] newBoard, int newCols, int newRows)
            {
                for (int row = 0; row < newRows && row < oldRows; ++row)
                {
                    for (int col = 0; col < newCols && col < oldCols; ++col)
                    {
                        newBoard[Index(col, row, newCols)] = oldBoard[Index(col, row, oldCols)];
                    }
                }

                // Make sure all new board tiles are not null.
                for (int row = 0; row < newRows; ++row)
                {
                    for (int col = 0; col < newCols; ++col)
                    {
                        int idx = Index(col, row, newCols);
                        if (null == newBoard[idx])
                        {
                            newBoard[idx] = new BoardTile
                            {
                                isFilled = false,
                                color = Color.white
                            };
                        }
                    }
                }
            }

            public void UpdateBoard()
            {
                if (!isPlaying) return;
                
                if (null != currentPiece)
                {
                    double elapsedTime = EditorApplication.timeSinceStartup - lastCurrentPieceMoveDownTime;
                    int blocksToMove = (int)(elapsedTime * FALL_SPEED);
                    //double remainingTime = elapsedTime - blocksToMove / adjustedFallSpeed;
                    //lastCurrentPieceMoveDownTime = EditorApplication.timeSinceStartup - remainingTime;

                    for (int i = 0; i < blocksToMove; ++i)
                    {
                        if (!MovePieceDown())
                        {
                            ++failedCurrentPieceDownAttempts;
                        }
                    }

                    if (failedCurrentPieceDownAttempts >= PIECE_DOWN_ATTEMPTS_BEFORE_LOCKING)
                    {
                        LockCurrentPiece();
                    }
                }
            }

            public void SlamPieceDown()
            {
                if (null == currentPiece) return;

                while (MovePieceDown()) ;

                LockCurrentPiece();
            }

            private void LockCurrentPiece()
            {
                if (null == currentPiece) return;

                if (ValidityResult.Valid != IsValidPosition(currentPiece))
                {
                    Debug.LogError("Current piece is in invalid position, can't lock piece.");
                    SpawnPiece();
                    return;
                }

                // Did we perform a t-spin?
                if (TetrisPiece.PieceType.T == currentPiece.pieceType)
                {
                    // If up, down, left, and right are invalid then we performed a t-spin.
                    if (
                        ValidityResult.Valid != IsValidPosition(currentPiece, 0, 1) &&
                        ValidityResult.Valid != IsValidPosition(currentPiece, 0, -1) &&
                        ValidityResult.Valid != IsValidPosition(currentPiece, 1, 0) &&
                        ValidityResult.Valid != IsValidPosition(currentPiece, -1, 0)
                        )
                    {
                        SetInfoMessage("T-Spin!", Color.cyan, 10);
                        ++tSpinCount;
                    }
                }

                // Copy piece contents to board.
                for (int row = currentPiece.row; row < currentPiece.row + 4; ++row)
                {
                    for (int col = currentPiece.col; col < currentPiece.col + 4; ++col)
                    {
                        if (currentPiece.IsHit(col, row))
                        {
                            BoardTile tile = gameBoard[Index(col, row, gameBoardCols)];
                            tile.isFilled = true;
                            tile.color = currentPiece.color;
                        }
                    }
                }

                ClearFullRows();

                SpawnPiece();
            }

            private void SpawnPiece()
            {
                currentPiece = new TetrisPiece();
                currentPiece.col = (gameBoardCols / 2);
                currentPiece.row = 0;
                lastCurrentPieceMoveDownTime = EditorApplication.timeSinceStartup;
                failedCurrentPieceDownAttempts = 0;

                if (ValidityResult.Valid != IsValidPosition(currentPiece))
                {
                    isPlaying = false;
                    SetInfoMessage("Game Over!", Color.red);
                    currentPiece = null;
                }
            }

            private void ClearFullRows()
            {
                int rowsClearedThisPass = 0;

                for (int row = gameBoardRows - 1; row >= 0; --row)
                {
                    // Is the row full?
                    bool rowIsFull = true;
                    for (int col = 0; col < gameBoardCols; ++col)
                    {
                        if (!gameBoard[Index(col, row, gameBoardCols)].isFilled)
                        {
                            rowIsFull = false;
                            continue;
                        }
                    }

                    if (rowIsFull)
                    {
                        ++totalRowsCleared;
                        ++rowsClearedThisPass;

                        // Delete row.
                        for (int col = 0; col < gameBoardCols; ++col)
                        {
                            gameBoard[Index(col, row, gameBoardCols)].isFilled = false;
                        }

                        // Shift everything above down a row, for each row above, copy it down.
                        for (int cpyRow = row - 1; cpyRow >= 0; --cpyRow)
                        {
                            for (int col = 0; col < gameBoardCols; ++col)
                            {
                                int targetRow = cpyRow + 1;

                                BoardTile targetCell = gameBoard[Index(col, targetRow, gameBoardCols)];
                                BoardTile cellToCopy = gameBoard[Index(col, cpyRow, gameBoardCols)];

                                targetCell.isFilled = cellToCopy.isFilled;
                                targetCell.color = cellToCopy.color;
                            }
                        }

                        // Clear the uppermost row (nothing to copy down into that).
                        for (int col = 0; col < gameBoardCols; ++col)
                        {
                            BoardTile targetCell = gameBoard[Index(col, 0, gameBoardCols)];
                            targetCell.isFilled = false;
                        }

                        ++row; // Adjust row index back one down so we continue checking other rows from the right spot.

                        // Note: future optimization, may be good to check if a clear row is hit when shifting down so we don't
                        // have to shift all the cells down every time. Then again this will happen when the screen is full anyway
                        // so maybe not the right optimization to focus on.
                    }
                }

                if (4 == rowsClearedThisPass)
                {
                    SetInfoMessage("Tetris!", Color.cyan, 10);
                }
            }

            public bool RotatePiece()
            {
                if (currentPiece == null) return true;

                currentPiece.RotatePiece();

                int initialCol = currentPiece.col;
                int initialRow = currentPiece.row;

                // Try offsetting the piece in combinations:
                //  0  0
                //  0  1
                //  0 -1
                //  1  0
                //  .  . (etc)
                // -1 -1
                // This can be mapped to counting in ternary.
                int[] offsets = new int[] { 0, 1, -1 };
                for (int rowOffset = 0; rowOffset < 3; ++rowOffset)
                {
                    for (int colOffset = 0; colOffset < 3; ++colOffset)
                    {
                        currentPiece.col = initialCol + offsets[colOffset];
                        currentPiece.row = initialRow + offsets[rowOffset];

                        if (ValidityResult.Valid == IsValidPosition(currentPiece))
                        {
                            failedCurrentPieceDownAttempts = Mathf.Max(0, failedCurrentPieceDownAttempts - 1);
                            return true;
                        }
                    }
                }

                currentPiece.col = initialCol;
                currentPiece.row = initialRow;

                currentPiece.RotatePieceReverse();

                return false;
            }

            public bool MovePieceRight()
            {
                if (null == currentPiece) return true;

                currentPiece.col += 1;
                if (ValidityResult.Valid != IsValidPosition(currentPiece))
                {
                    currentPiece.col -= 1;
                    return false;
                }

                failedCurrentPieceDownAttempts = Mathf.Max(0, failedCurrentPieceDownAttempts - 1);

                return true;
            }

            public bool MovePieceLeft()
            {
                if (null == currentPiece) return true;

                currentPiece.col -= 1;
                if (ValidityResult.Valid != IsValidPosition(currentPiece))
                {
                    currentPiece.col += 1;
                    return false;
                }

                failedCurrentPieceDownAttempts = Mathf.Max(0, failedCurrentPieceDownAttempts - 1);

                return true;
            }

            public bool MovePieceDown()
            {
                if (null == currentPiece) return true;

                lastCurrentPieceMoveDownTime = EditorApplication.timeSinceStartup;

                currentPiece.row += 1;
                if (ValidityResult.Valid != IsValidPosition(currentPiece))
                {
                    currentPiece.row -= 1;
                    return false;
                }
                return true;
            }

            public ValidityResult IsValidPosition(TetrisPiece piece)
            {
                for (int row = piece.row; row < piece.row + 4; ++row)
                {
                    for (int col = piece.col; col < piece.col + 4; ++col)
                    {
                        if (piece.IsHit(col, row))
                        {
                            if (col >= gameBoardCols || col < 0 || row >= gameBoardRows || row < 0)
                            {
                                return ValidityResult.OutOfBounds;
                            }
                            if (gameBoard[Index(col, row, gameBoardCols)].isFilled)
                            {
                                return ValidityResult.IntersectsBlock;
                            }
                        }
                    }
                }

                return ValidityResult.Valid;
            }

            public ValidityResult IsValidPosition(TetrisPiece piece, int colOffset, int rowOffset)
            {
                piece.col += colOffset;
                piece.row += rowOffset;

                ValidityResult result = IsValidPosition(piece);

                piece.col -= colOffset;
                piece.row -= rowOffset;

                return result;
            }

            public int GetRowsCleared()
            {
                return totalRowsCleared;
            }

            public int GetTSpinCount()
            {
                return tSpinCount;
            }
        }

        GameBoard gameBoard;
        bool keyIsDown = false;
        float infoPanelWidthPx = 200;

        [MenuItem("mSubspace/Games/Tetris")]
        public static void Init()
        {
            TetrisEditor w = GetWindow<TetrisEditor>();
            w.titleContent = new GUIContent("Tetris?!");
            w.Show();
        }

        private void OnEnable()
        {
            if (null == gameBoard)
            {
                gameBoard = new GameBoard(new Rect(0, 0, position.width, position.height));
            }
        }

        private void Update()
        {
            gameBoard.UpdateBoard();
            Repaint();
        }

        private void OnGUI()
        {
            Rect infoPanelRect = new Rect(position.width - infoPanelWidthPx, 0, infoPanelWidthPx, position.height);
            Rect gameBoardRect = new Rect(0, 0, position.width - infoPanelWidthPx, position.height);

            gameBoard.Draw(gameBoardRect);

            GUILayout.BeginArea(infoPanelRect);
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Rows Cleared", EditorStyles.boldLabel);

                GUI.backgroundColor = Color.grey;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    EditorGUILayout.LabelField(gameBoard.GetRowsCleared().ToString(), EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;


                EditorGUILayout.Space();
                EditorGUILayout.LabelField("T-Spin Count", EditorStyles.boldLabel);

                GUI.backgroundColor = Color.grey;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    EditorGUILayout.LabelField(gameBoard.GetTSpinCount().ToString(), EditorStyles.boldLabel);
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;


                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Info Message", EditorStyles.boldLabel);

                GUI.backgroundColor = Color.grey;
                EditorGUILayout.BeginHorizontal(EditorStyles.helpBox);
                {
                    GameBoard.InfoMessage message = gameBoard.GetInfoMessage();
                    if (message.IsNotExpired())
                    {
                        GUI.contentColor = message.color;
                        EditorGUILayout.LabelField(message.message, EditorStyles.boldLabel);
                        GUI.contentColor = Color.white;
                    }
                    else
                    {
                        EditorGUILayout.LabelField("");
                    }
                }
                EditorGUILayout.EndHorizontal();
                GUI.backgroundColor = Color.white;

                EditorGUILayout.Space();
                if (GUILayout.Button("Reset Game"))
                {
                    gameBoard.Reset();
                }

            }
            GUILayout.EndArea();

            GUI.FocusControl(null);

            // Repeated events while key is held down (left/right movement)
            if (Event.current.rawType == EventType.KeyDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.RightArrow:
                        {
                            gameBoard.MovePieceRight();
                            break;
                        }
                    case KeyCode.LeftArrow:
                        {
                            gameBoard.MovePieceLeft();
                            break;
                        }
                    case KeyCode.DownArrow:
                        {
                            gameBoard.MovePieceDown();
                            break;
                        }
                }
            }

            // Single press events, only once on key down.
            if (Event.current.rawType == EventType.KeyDown && !keyIsDown)
            {
                switch (Event.current.keyCode)
                {
                    case KeyCode.UpArrow:
                        {
                            gameBoard.RotatePiece();
                            break;
                        }
                    case KeyCode.Space:
                    case KeyCode.RightControl:
                    case KeyCode.LeftControl:
                        {
                            gameBoard.SlamPieceDown();
                            break;
                        }
                }

                keyIsDown = true;
            }
            else if (Event.current.rawType == EventType.KeyUp)
            {
                keyIsDown = false;
            }
        }

        public static int Index(int col, int row, int width)
        {
            return col + row * width;
        }
    }
}
