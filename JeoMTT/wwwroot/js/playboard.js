/* PlayBoard Question Management */

(function() {
    let quillEditor;

    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', initializePlayBoard);

    function initializePlayBoard() {
        const modal = document.getElementById('questionModal');
        const gameId = modal?.dataset.gameId;
        if (!gameId) return;

        initializeQuillEditor();
        setupQuestionModal(gameId);
        setupQuestionButtons();
        trackPageView(gameId);
    }    function initializeQuillEditor() {        // Initialize Quill editor with restricted formats
        quillEditor = new Quill('#questionEditor', {
            theme: 'snow',
            placeholder: 'Enter your question here...',
            modules: {
                toolbar: [
                    ['bold', 'italic', 'underline'],
                    [{ 'list': 'ordered'}, { 'list': 'bullet' }],
                    ['clean']
                ],
                clipboard: {
                    matchVisibility: false,
                    matchers: [
                        // Block images
                        ['img', function() {
                            return '';
                        }]
                    ]
                }
            },
            formats: ['bold', 'italic', 'underline', 'list']
        });        // Update character count on text change
        quillEditor.on('text-change', function() {
            const text = quillEditor.getText().trim();
            const charCount = document.getElementById('charCount');
            charCount.textContent = Math.min(text.length, 200);

            // Enforce 200 character limit
            if (text.length > 200) {
                const delta = quillEditor.getContents();
                quillEditor.setContents(delta, 'silent');
                quillEditor.setSelection(200);
            }
        });

        // Prevent paste of images and non-text content
        quillEditor.root.addEventListener('paste', function(e) {
            e.preventDefault();
            const text = (e.clipboardData || window.clipboardData).getData('text/plain');
            quillEditor.insertText(quillEditor.getSelection().index, text);
        }, false);
    }function setupQuestionModal(gameId) {
        const questionModal = new bootstrap.Modal(document.getElementById('questionModal'), {});
        const questionForm = document.getElementById('questionForm');
        const questionTextInput = document.getElementById('questionText');
        const answerText = document.getElementById('answerText');
        const categoryIdInput = document.getElementById('categoryId');
        const pointsInput = document.getElementById('points');
        const charCount = document.getElementById('charCount');
        const questionError = document.getElementById('questionError');
        const saveQuestionBtn = document.getElementById('saveQuestionBtn');

        // Save question
        saveQuestionBtn.addEventListener('click', async () => {
            await saveQuestion(categoryIdInput, pointsInput, answerText, questionError, questionModal);
        });

        // Handle modal close
        document.getElementById('questionModal').addEventListener('hide.bs.modal', () => {
            quillEditor.setContents([]);
            answerText.value = '';
            charCount.textContent = '0';
            questionError.style.display = 'none';
        });

        window.questionModalInstance = questionModal;
    }

    function setupQuestionButtons() {
        document.querySelectorAll('.question-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                await loadQuestion(btn);
            });
        });
    }    async function loadQuestion(btn) {
        const currentCategoryId = btn.dataset.categoryId; // Keep as string (Guid)
        const currentPoints = parseInt(btn.dataset.points);

        const answerText = document.getElementById('answerText');
        const categoryIdInput = document.getElementById('categoryId');
        const pointsInput = document.getElementById('points');
        const charCount = document.getElementById('charCount');
        const questionError = document.getElementById('questionError');

        categoryIdInput.value = currentCategoryId;
        pointsInput.value = currentPoints;

        // Clear editor
        quillEditor.setContents([]);
        charCount.textContent = '0';

        // Load existing question if it exists
        try {
            const response = await fetch(`/JeoGame/GetQuestion?categoryId=${currentCategoryId}&points=${currentPoints}`);
            if (response.ok) {
                const data = await response.json();
                if (data.text) {
                    // Set Quill editor content with plain text
                    quillEditor.setText(data.text);
                    charCount.textContent = data.text.length;
                }
                answerText.value = data.answer || '';
            }
        } catch (error) {
            console.error('Error loading question:', error);
            if (window.trackEvent) {
                trackEvent('QuestionLoadError', { 'error': error.message });
            }
        }

        questionError.style.display = 'none';
        window.questionModalInstance?.show();
    }async function saveQuestion(categoryIdInput, pointsInput, answerText, questionError, questionModal) {
        // Extract text from Quill editor
        const qText = quillEditor.getText().trim();
        const aText = answerText.value.trim();
        const currentCategoryId = categoryIdInput.value; // This is already a Guid string
        const currentPoints = parseInt(pointsInput.value);

        // Validation
        if (!qText) {
            showError(questionError, 'Question is required');
            return;
        }

        if (qText.length > 200) {
            showError(questionError, 'Question cannot exceed 200 characters');
            return;
        }

        if (!aText) {
            showError(questionError, 'Answer is required');
            return;
        }        try {
            const token = document.querySelector('[name="__RequestVerificationToken"]')?.value;
            
            const response = await fetch('/JeoGame/SaveQuestion', {
                method: 'POST',
                headers: {
                    'Content-Type': 'application/x-www-form-urlencoded',
                    'RequestVerificationToken': token || ''
                },
                body: new URLSearchParams({
                    'categoryId': currentCategoryId,
                    'points': currentPoints.toString(),
                    'questionText': qText,
                    'answerText': aText,
                    '__RequestVerificationToken': token || ''
                })
            });

            if (response.ok) {
                if (window.trackEvent) {
                    trackEvent('QuestionSaved', {
                        'categoryId': currentCategoryId.toString(),
                        'points': currentPoints.toString()
                    });
                }

                questionModal.hide();
                // Reload page to show updated board
                location.reload();
            } else {
                const error = await response.text();
                console.error('Save failed:', response.status, error);
                showError(questionError, error || 'Failed to save question');
            }
        } catch (error) {
            showError(questionError, 'Error: ' + error.message);
            if (window.trackEvent) {
                trackEvent('QuestionSaveError', { 'error': error.message });
            }
        }
    }

    function showError(errorElement, message) {
        errorElement.textContent = message;
        errorElement.style.display = 'block';
    }

    function trackPageView(gameId) {
        if (window.trackEvent) {
            const categoryCount = document.querySelectorAll('.category-header').length;
            trackEvent('PlayBoardPageViewed', {
                'gameId': gameId.toString(),
                'categoryCount': categoryCount.toString()
            });
        }
    }
})();
