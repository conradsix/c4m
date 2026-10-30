-- Helper functions for path management

-- Function to build a full path from a page ID
CREATE OR REPLACE FUNCTION cms.build_page_path(page_id UUID)
RETURNS TEXT AS $$
DECLARE
    current_id UUID := page_id;
    parent_id_val UUID;
    current_slug TEXT;
    path_parts TEXT[] := '{}';
    full_path TEXT;
BEGIN
    -- First, get the page's own slug and parent_id
    SELECT pages.slug, pages.parent_id
    INTO current_slug, parent_id_val
    FROM cms.pages 
    WHERE pages.id = page_id;
    
    -- If page not found or slug is empty, return empty path
    IF NOT FOUND OR current_slug IS NULL OR current_slug = '' THEN
        RETURN '';
    END IF;
    
    -- Start with this page's slug
    path_parts := ARRAY[current_slug];
    current_id := parent_id_val;
    
    -- Walk up the hierarchy collecting slugs from parents
    WHILE current_id IS NOT NULL LOOP
        SELECT pages.slug, pages.parent_id 
        INTO current_slug, parent_id_val
        FROM cms.pages 
        WHERE pages.id = current_id;
        
        -- If parent not found or slug is empty, break
        IF NOT FOUND OR current_slug IS NULL OR current_slug = '' THEN
            EXIT;
        END IF;
        
        -- Prepend parent slug to path parts
        path_parts := current_slug || path_parts;
        
        -- Move to next parent
        current_id := parent_id_val;
    END LOOP;
    
    -- Build final path
    full_path := '/' || array_to_string(path_parts, '/');
    
    RETURN full_path;
END;
$$ LANGUAGE plpgsql;

-- Function to update path for a specific page
CREATE OR REPLACE FUNCTION cms.update_page_path(page_id UUID)
RETURNS VOID AS $$
DECLARE
    new_path TEXT;
BEGIN
    -- Calculate the new path
    new_path := cms.build_page_path(page_id);
    
    -- Update the page
    UPDATE cms.pages 
    SET path = new_path 
    WHERE pages.id = page_id;
END;
$$ LANGUAGE plpgsql;

-- Function to recursively update paths for all children of a page
CREATE OR REPLACE FUNCTION cms.update_children_paths(parent_page_id UUID)
RETURNS VOID AS $$
DECLARE
    child_record RECORD;
BEGIN
    -- Update all direct children
    FOR child_record IN 
        SELECT id FROM cms.pages 
        WHERE pages.parent_id = parent_page_id
    LOOP
        -- Update this child's path
        PERFORM cms.update_page_path(child_record.id);
        
        -- Recursively update this child's children
        PERFORM cms.update_children_paths(child_record.id);
    END LOOP;
END;
$$ LANGUAGE plpgsql;
