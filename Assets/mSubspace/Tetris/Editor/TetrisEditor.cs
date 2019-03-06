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
            public readonly bool[,] PIECE_MAPS = new bool[,]
            {
                // T Piece
                {
                    false, false, false, false,
                    false, false, false, false,
                    true,   true,  true, false,
                    false,  true, false, false
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
                    false,  false,  true, true,
                    false,  true,  true, false,
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

            private readonly int[] rotationMapping = new int[]
            {
                12,  8, 4, 0,
                13,  9, 5, 1,
                14, 10, 6, 2,
                15, 11, 7, 3
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

            public TetrisPiece()
            {
                // Construct with a random piece.
                int randPiece = (int)(Random.value * PIECE_MAPS.GetLength(0));
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
                    rotatedPiece[i] = pieceData[rotationMapping[i]];
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
            public const int BLOCK_PIXEL_SIZE = 14; // Around the size of a toggle button.

            Rect gameBoardRect;

            int gameBoardRows = 0;
            int gameBoardCols = 0;

            int gameBoardWidth;
            int gameBoardHeight;

            float xLeftOffset;
            float yTopOffset;

            class BoardTile
            {
                public bool isFilled;
                public Color color;
            }

            BoardTile[] gameBoard = new BoardTile[0];
            TetrisPiece currentPiece = null;

            readonly float FALL_SPEED = 2f; // blocks per second.
            readonly int PIECE_DOWN_ATTEMPTS_BEFORE_LOCKING = 2;

            double lastCurrentPieceMoveDownTime = 0f;
            int failedCurrentPieceDownAttempts = 0;

            public GameBoard(Rect gameBoardRect)
            {
                UpdateBoardLayout(gameBoardRect);
                SpawnPiece();
            }

            public void Draw(Rect gameBoardRect)
            {
                UpdateBoardLayout(gameBoardRect);

                for (int row = 0; row < gameBoardRows; ++row)
                {
                    for (int col = 0; col < gameBoardCols; ++col)
                    {
                        //Rect blockRect = new Rect(xLeftOffset + col * BLOCK_PIXEL_SIZE, yTopOffset + row * BLOCK_PIXEL_SIZE, BLOCK_PIXEL_SIZE, BLOCK_PIXEL_SIZE);
                        Rect checkboxRect = new Rect(xLeftOffset + col * BLOCK_PIXEL_SIZE, yTopOffset + row * BLOCK_PIXEL_SIZE, BLOCK_PIXEL_SIZE, BLOCK_PIXEL_SIZE);
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

                        EditorGUI.Toggle(checkboxRect, tile.isFilled || currentPieceHit);
                        GUI.color = Color.white;
                    }
                }
            }

            private void UpdateBoardLayout(Rect gameBoardRect)
            {
                this.gameBoardRect = gameBoardRect;

                float fullAreaWidth = gameBoardRect.width;
                float fullAreaHeight = gameBoardRect.height;

                int newGameBoardCols = (int)(fullAreaWidth / BLOCK_PIXEL_SIZE);
                int newGameBoardRows = (int)(fullAreaHeight / BLOCK_PIXEL_SIZE);

                if (newGameBoardRows != gameBoardRows || newGameBoardCols != gameBoardCols)
                {
                    gameBoardWidth = newGameBoardCols * BLOCK_PIXEL_SIZE;
                    gameBoardHeight = newGameBoardRows * BLOCK_PIXEL_SIZE;

                    xLeftOffset = gameBoardRect.x + (fullAreaWidth - gameBoardWidth) / 2.0f;
                    yTopOffset = gameBoardRect.y + (fullAreaHeight - gameBoardHeight) / 2.0f;

                    BoardTile[] newGameBoard = new BoardTile[newGameBoardCols * newGameBoardRows];
                    Copy(ref gameBoard, gameBoardCols, gameBoardRows, ref newGameBoard, newGameBoardCols, newGameBoardRows);
                    gameBoard = newGameBoard;

                    gameBoardCols = newGameBoardCols;
                    gameBoardRows = newGameBoardRows;
                }
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
                if (null != currentPiece)
                {
                    double elapsedTime = EditorApplication.timeSinceStartup - lastCurrentPieceMoveDownTime;
                    int blocksToMove = (int)(elapsedTime * FALL_SPEED);
                    double remainingTime = elapsedTime - blocksToMove / FALL_SPEED;
                    lastCurrentPieceMoveDownTime = EditorApplication.timeSinceStartup - remainingTime;

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
                while (MovePieceDown()) ;
                LockCurrentPiece();
            }

            private void LockCurrentPiece()
            {
                if (!IsValidPosition(currentPiece))
                {
                    Debug.LogError("Current piece is in invalid position, can't lock piece.");
                    SpawnPiece();
                    return;
                }

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

                SpawnPiece();
            }

            private void SpawnPiece()
            {
                currentPiece = new TetrisPiece();
                currentPiece.col = (gameBoardCols / 2);
                currentPiece.row = 0;
                lastCurrentPieceMoveDownTime = EditorApplication.timeSinceStartup;
                failedCurrentPieceDownAttempts = 0;
            }

            public bool HasActivePiece()
            {
                return currentPiece != null;
            }

            public void RotatePiece()
            {
                if (currentPiece != null) currentPiece.RotatePiece();
            }

            public bool MovePieceRight()
            {
                currentPiece.col += 1;
                if (!IsValidPosition(currentPiece))
                {
                    currentPiece.col -= 1;
                    return false;
                }

                failedCurrentPieceDownAttempts = Mathf.Max(0, failedCurrentPieceDownAttempts - 1);

                return true;
            }

            public bool MovePieceLeft()
            {
                currentPiece.col -= 1;
                if (!IsValidPosition(currentPiece))
                {
                    currentPiece.col += 1;
                    return false;
                }

                failedCurrentPieceDownAttempts = Mathf.Max(0, failedCurrentPieceDownAttempts - 1);

                return true;
            }

            private bool MovePieceDown()
            {
                currentPiece.row += 1;
                if (!IsValidPosition(currentPiece))
                {
                    currentPiece.row -= 1;
                    return false;
                }
                return true;
            }

            public bool IsValidPosition(TetrisPiece piece)
            {
                for (int row = piece.row; row < piece.row + 4; ++row)
                {
                    for (int col = piece.col; col < piece.col + 4; ++col)
                    {
                        if (piece.IsHit(col, row))
                        {
                            if ( col >= gameBoardCols || col < 0 || row >= gameBoardRows || row < 0 ||
                                 gameBoard[Index(col, row, gameBoardCols)].isFilled)
                            {
                                return false;
                            }
                        }
                    }
                }

                return true;
            }
        }

        GameBoard gameBoard;

        bool keyIsDown = false;

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
            gameBoard.Draw(new Rect(0, 0, position.width, position.height));

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
                    case KeyCode.DownArrow:
                        {
                            gameBoard.SlamPieceDown();
                            break;
                        }
                }

                keyIsDown = true;
                Event.current.Use();
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
