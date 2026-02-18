/* Manage Categories */

(function() {
    // Initialize when DOM is ready
    document.addEventListener('DOMContentLoaded', initializeManageCategories);    function initializeManageCategories() {
        const form = document.getElementById('addCategoryForm');
        const gameId = form?.dataset.gameId;
        if (!gameId) return;

        setupAddCategoryForm(gameId);
        setupRemoveCategoryButtons(gameId);
        trackPageView(gameId);
    }

    function setupAddCategoryForm(gameId) {
        const form = document.getElementById('addCategoryForm');
        const categoryNameInput = document.getElementById('categoryName');
        const errorDiv = document.getElementById('categoryError');

        form.addEventListener('submit', async (e) => {
            e.preventDefault();

            const categoryName = categoryNameInput.value.trim();

            if (!categoryName) {
                showError(errorDiv, 'Group name is required');
                return;
            }

            // Check for duplicate category name
            const existingCategories = document.querySelectorAll('.category-name');
            const isDuplicate = Array.from(existingCategories).some(cat => 
                cat.textContent.trim().toLowerCase() === categoryName.toLowerCase()
            );

            if (isDuplicate) {
                showError(errorDiv, `Group name "${categoryName}" already exists. Please use a different name.`);
                return;
            }

            try {
                const response = await fetch('/JeoGame/AddCategory', {
                    method: 'POST',
                    headers: {
                        'Content-Type': 'application/x-www-form-urlencoded',
                        'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]')?.value || ''
                    },
                    body: new URLSearchParams({
                        'gameId': gameId,
                        'categoryName': categoryName
                    })
                });

                if (response.ok) {
                    if (window.trackEvent) {
                        trackEvent('CategoryAdded', {
                            'gameId': gameId.toString(),
                            'categoryName': categoryName
                        });
                    }

                    // Reload the page to show the new category
                    location.reload();
                } else {
                    const errorText = await response.text();
                    showError(errorDiv, errorText || 'Failed to add group');
                }
            } catch (error) {
                showError(errorDiv, 'Error adding group: ' + error.message);
                if (window.trackEvent) {
                    trackEvent('CategoryAddError', {
                        'error': error.message
                    });
                }
            }
        });
    }    function setupRemoveCategoryButtons(gameId) {
        document.querySelectorAll('.remove-category-btn').forEach(btn => {
            btn.addEventListener('click', async (e) => {
                if (!confirm('Are you sure you want to remove this group? This will also delete all questions in it.')) {
                    return;
                }

                const categoryId = btn.dataset.categoryId;
                const categoryName = btn.dataset.categoryName;

                try {
                    const response = await fetch('/JeoGame/RemoveCategory', {
                        method: 'POST',
                        headers: {
                            'Content-Type': 'application/x-www-form-urlencoded',
                            'RequestVerificationToken': document.querySelector('[name="__RequestVerificationToken"]')?.value || ''
                        },
                        body: new URLSearchParams({
                            'categoryId': categoryId,
                            'gameId': gameId
                        })
                    });

                    if (response.ok) {
                        if (window.trackEvent) {
                            trackEvent('CategoryRemoved', {
                                'categoryId': categoryId.toString(),
                                'categoryName': categoryName
                            });
                        }

                        // Reload the page to reflect removal
                        location.reload();
                    } else {
                        const errorText = await response.text();
                        alert('Failed to remove group: ' + (errorText || 'Unknown error'));
                    }
                } catch (error) {
                    alert('Error removing group: ' + error.message);
                    if (window.trackEvent) {
                        trackEvent('CategoryRemoveError', {
                            'error': error.message
                        });
                    }
                }
            });
        });
    }

    function showError(errorElement, message) {
        errorElement.textContent = message;
        errorElement.style.display = 'block';
    }

    function trackPageView(gameId) {
        if (window.trackEvent) {
            const categoryCount = document.querySelectorAll('.card.border-success').length;
            trackEvent('ManageCategoriesPageViewed', {
                'gameId': gameId.toString(),
                'categoryCount': categoryCount.toString()
            });
        }
    }
})();
